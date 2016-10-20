using System;
using System.Collections;

namespace Babel.Core {

    public class StringSplitter {

        static readonly string RejectChars = "=+-*%<>&|^/{.\\";

        public delegate bool IsValidNameDelegate(string name);

        IsValidNameDelegate Validator;
        string Buffer;
        bool IsPastEnd;
        int BufferIndex;

        public StringSplitter(IsValidNameDelegate validator=null) {
            Validator = validator;
        }

        public static bool IsWhitespace(char chr) {
            return (chr == ' ') || (chr == '\t') || (chr == '\n');
        }

        public static bool IsAlpha(char chr) {
            return ((chr >= 'a') && (chr <= 'z')) || ((chr >= 'A') && (chr <= 'Z')) || (chr == '_');
        }

        public static bool IsIdent(char chr) {
            return IsAlpha(chr) || ((chr >= '0') && (chr <= '9'));
        }

        public void SetBuffer(string buffer) {
            Buffer = buffer;
        }
        public string GetBuffer() {
            return Buffer;
        }

        // Get a character from buffer.
        // Returns next chr, or 0 if at end of buffer.
        char InputChar() {
            if (BufferIndex < Buffer.Length) return Buffer[BufferIndex++];
            IsPastEnd = true;
            return '\0';
        }

        char PeekChar() {
            if (BufferIndex == Buffer.Length) return '\0';
            return Buffer[BufferIndex];
        }

        // Wind back the buffer pt.
        void UnputChar() {
            if (IsPastEnd) IsPastEnd = false;
            else if (BufferIndex > 0) --BufferIndex;
        }

        // Skip whitespace.
        // Returns number of whitespace chars that were skipped.
        void SkipWhitespace() {
            for (; ; ) {
                switch (PeekChar()) {
                    case ' ':
                    case '\t':
                    case '\n':
                        InputChar();
                        break;
                    default:
                        return;
                }
            }
        }

        char PeekCharAfterWhitespace() {
            int saveIndex = BufferIndex;
            bool savePastEnd = IsPastEnd;
            SkipWhitespace();
            char c = PeekChar();
            BufferIndex = saveIndex;
            IsPastEnd = savePastEnd;
            return c;
        }

        void SkipWhitespaceAndComma() {
            bool c = false;
            for (; ; ) {
                switch (PeekChar()) {
                    case ',':
                        if (c) return;
                        c = true;
                        break;
                    case ' ':
                    case '\t':
                    case '\n':
                        break;
                    default:
                        return;
                }
                InputChar();
            }
        }

        /* Skip anything in single or double quotes.
        *  Returns first non quoted chr. Newline and EOF terminate it as well. */
        char SkipQuoted() {
            char chr = '\n', laschr; bool dquote = false, squote = false;
            do {
                laschr = chr;
                SkipWhitespace();
                chr = InputChar();
                switch (chr) {
                    case '\0': return (chr);
                    case '"': if (!squote && laschr != '\\') dquote = (bool)!dquote;
                        break;
                    case '\'': if (!dquote && laschr != '\\') squote = (bool)!squote;
                        break;
                    default: break;
                }
            } while (dquote || squote);
            return (chr);
        }

        void SkipToWhitespaceOrCommaOrSemiColon() {
            char chr;
            for (; ; ) {
                chr = PeekChar();
                if ((chr == '"') || (chr == '\'')) {
                    chr = SkipQuoted();
                    if ((chr == '"') || (chr == '\'')) continue;
                }
                if (IsWhitespace(chr) || (chr == '\0') || (chr == ',')||(chr==';')) return;
                InputChar();
            }
        }

        /* Skip over a matching pair.
        *  Returns first chr if not lpair, or rpair if found. */
        char SkipPair(char lpair, char rpair) {
            int pair = 0; char chr;
            do {
                chr = SkipQuoted();
                if (chr == lpair) ++pair; else if (chr == rpair) --pair;
            } while ((pair != 0) && (chr != '\0'));
            return (chr);
        }

        string GetIdent() {
            char chr;
            SkipWhitespace();
            chr = InputChar();
            if (IsAlpha(chr)) {
                int istart = BufferIndex - 1;
                do {
                    chr = InputChar();
                } while (IsIdent(chr));
                UnputChar();
                return Buffer.Substring(istart, BufferIndex - istart);
            }
            return null;
        }

        // Allows indent to be prefixed by "./".
        string GetRelativeIdent() {
            char chr;
            SkipWhitespace();
            chr = InputChar();
            if (IsAlpha(chr)||(chr=='.')) {
                int istart = BufferIndex - 1;
                bool isRelative = (chr=='.');
                bool isFirst = true;
                do {
                    chr = InputChar();
                    if (isFirst) {
                        isFirst = false;
                        if (isRelative && (chr == '/' || chr == '\\')) {
                            continue;
                        }
                    }
                } while (IsIdent(chr));
                UnputChar();
                return Buffer.Substring(istart, BufferIndex - istart);
            }
            return null;
        }

        // Gets the next arg.
        // Returns arg or null if none left.
        // BufferIndex points to next char after arg.
        string GetArg() {
            char chr; int len, istart;
            SkipWhitespaceAndComma();
            istart = BufferIndex;
            chr = PeekChar();
            switch (chr) {
                case '\0':
                case ';':
                    return null;
                case '(':
                    chr = SkipPair('(', ')');
                    chr = PeekChar();
                    if ((chr != '\0') && (chr != ',') && (chr!=';') && !IsWhitespace(chr)) SkipToWhitespaceOrCommaOrSemiColon();
                    break;
                default:
                    SkipToWhitespaceOrCommaOrSemiColon();
                    break;
            }
            len = BufferIndex - istart;
            if (len > 0)
                return Buffer.Substring(istart, len);
            else if (PeekChar() == ',') return "";
            return null;
        }

        // Split args: ident [ {whitespace|','} exp ]* [;] [\n]
        // where exp = { num, ident, quotedString, singleQuotedString, arithexp }.
        // arithexp = { '-'arith, '('...')', {num|ident}{opr}* }.
        // Returns null if syntax doesn't conform to required pattern.
        // Rejects line if char after ident is in RejectChars.
        // Note: buffer must be a single line.
        public ArrayList Split(string buffer) {
            if (buffer == null) return null;
            Buffer = buffer;
            BufferIndex = 0;
            IsPastEnd = false;
            string arg = GetIdent();
            if (arg == null) return null;
            if ((Validator != null) && !Validator(arg)) return null;
            char chr = PeekChar();
            if ((!IsWhitespace(chr)) && (chr != ';') && (chr != '\0')) return null;
            chr = PeekCharAfterWhitespace();
            if (RejectChars.IndexOf(chr) >= 0) return null;
            ArrayList args = new ArrayList();
            args.Add(arg);
            while ((arg = GetArg()) != null) args.Add(arg);
            return args;
        }

        public ArrayList SplitArgs(string buffer) {
            if (buffer == null) return null;
            Buffer = buffer;
            BufferIndex = 0;
            IsPastEnd = false;
            string arg;
            ArrayList args = new ArrayList();
            while ((arg = GetArg()) != null) args.Add(arg);
            return args;
        }
    }
}
