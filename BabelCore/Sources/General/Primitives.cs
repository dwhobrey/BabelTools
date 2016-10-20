using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Babel.Core {

    public class Primitives {

        public static String TAG = "Primitives";

        public static long BabelMilliTickerStart = DateTime.UtcNow.Ticks / 10000;

        public static uint GetBabelMilliTicker() {
            return (uint)((DateTime.UtcNow.Ticks / 10000) - BabelMilliTickerStart);
        }

        // Returns the negated byte.
        public static byte ByteNeg(byte n) {
            return (byte)~n;
        }
        // Returns the negated low nibble.
        public static byte NibbleNeg(byte n) {
            return (byte)(~n & 0xf);
        }
        // Returns the high nibble as a low nibble.
        public static byte NibbleHigh(byte n) {
            return (byte)(n >> 4);
        }
        // Converts low nibble to high.
        public static byte NibbleToHigh(byte n) {
            return (byte)(n << 4);
        }
        public static byte NibbleToPid(byte n) {
            return (byte)(((~n & 0xf)<<4)+(n&0xf));
        }
        public static byte NibbleToHexChar(byte n) {
            return (byte)(n + ((n <= 9) ? '0' : '7'));
        }

        public static bool IsValidFilename(String s) {
            int k, n;
            if (s == null) return false;
            n = s.Length;
            for (k = 0; k < n; k++) {
                char c = s[k];
                if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '.' || c == ' ') continue;
                return false;
            }
            return true;
        }

        public static String StripFilenameExtension(String s) {
            int n;
            s = s.Trim();
            n = s.LastIndexOf('.');
            if (n >= 0) {
                if (n == 0)
                    return null;
                int k = s.Length;
                if ((k - n) > 4) return s; // check for url's with web addresses.
                return s.Substring(0, n);
            }
            return s;
        }

        public static String AddXmlFilenameExtension(String s) {
            s = StripFilenameExtension(s);
            if (s != null)
                return s + ".xml";
            return null;
        }

        public static String GetFilenameWithExtension(String s) {
            int n = s.LastIndexOf('/');
            if (n > 0 && ((n + 1) < s.Length)) {
                s = s.Substring(n + 1).Trim();
                if (s.Length > 0) return s;
            }
            return null;
        }

        public static int StringToInt(String s) {
            int v;
            if (int.TryParse(s, out v)) return v;
            return 0;
        }

        public static byte[] StringToByteArray(String s) {
            byte[] q = null;
            try {
                q = Encoding.UTF8.GetBytes(s);
            } catch (Exception) {
            }
            return q;
        }

        public static byte[] ByteToByteArray(byte v) {
            return new byte[] { v };
        }

        public static byte[] UShortToByteArray(ushort v) {
            return new byte[] {
                (byte)(v&0xff),
	            (byte)(v >> 8)
	            };
        }

        // Little Endian: i.e. LSB first.
        public static byte[] ShortToByteArray(short v) {
            return new byte[] {
                (byte)(v&0xff),
	            (byte)((v&0xffff) >> 8)
	            };
        }

        public static byte[] UIntToByteArray(uint v) {
            int k, len = 4;
            byte[] tmp = new byte[len];
            for (k = 0; k < len; k++) {
                tmp[k] = (byte)(v & 0xff);
                v >>= 8;
            }
            return tmp;
        }

        // Little Endian: i.e. LSB first.
        public static byte[] IntToByteArray(int v) {
            int k, len = 4;
            byte[] tmp = new byte[len];
            for (k = 0; k < len; k++) {
                tmp[k] = (byte)(v & 0xff);
                v >>= 8;
            }
            return tmp;
        }

        // Little Endian: i.e. LSB first.
        public static byte[] LongToByteArray(long v) {
            int k, len = 8;
            byte[] tmp = new byte[len];
            for (k = 0; k < len; k++) {
                tmp[k] = (byte)(v & 0xff);
                v >>= 8;
            }
            return tmp;
        }

        // Little Endian: i.e. LSB first.
        public static long GetArrayValueS(byte[] ary, int idx, int len) {
            ulong v = 0,b=(((ulong)0x1)<<(len*8-1));
            int n = len;
            while (len-- > 0) {
                v = (v << 8) | ary[idx + len];
            }
            if ((v & b) != 0) {
                b = 0xff;
                while(n-->1) b=(b<<8)|0xff;
                return -((long)((~v)&b)) - 1;
            }
            return (long)v;
        }

        public static ulong GetArrayValueU(byte[] ary, int idx, int len) {
            ulong v = 0;
            while (len-- > 0)
                v = (v << 8) | ary[idx + len];
            return v;
        }

        // Little Endian: i.e. LSB first.
        public static void SetArrayValue(long v, byte[] ary, int idx, int len) {
            int k;
            for (k = 0; k < len; k++) {
                ary[idx + k] = (byte)(v & 0xff);
                v >>= 8;
            }
        }

        public static void SetArrayFloat(float v, byte[] ary, int idx, int len) {
            int k;
            byte[] b = BitConverter.GetBytes(v);
            for (k = 0; k < len; k++) {
                ary[idx + k] = b[k];
            }
        }

        // Copy string to array and add trailing zero if space.
        // Returns number of bytes written.
        public static int SetArrayString(String v, byte[] ary, int idx) {
            int k, n = 0;
            byte[] p = null;
            try {
                p = Encoding.UTF8.GetBytes(v);
            } catch (Exception) {
            }
            if (p != null) {
                int len = ary.Length;
                n = p.Length;
                for (k = 0; k < n; k++) {
                    if ((idx + k) >= len) break;
                    ary[idx + k] = p[k];
                }
                if((idx+n)<len)
                    ary[idx + n++] = 0;
            } else {
                ary[idx] = 0;
                n = 1;
            }
            return n;
        }

        // Stores string into array prefixed with len byte.
        public static int SetArrayStringValue(String v, byte[] ary, int idx) {
            int k, len = 0;
            if (v != null) len = v.Length;
            ary[idx++] = (byte)(len & 0xff);
            for (k = 0; k < len; k++) {
                ary[idx++] = (byte)(v[k] & 0xff);
            }
            return len;
        }

        // Get string from array up to size or first zero char.
        public static String GetArrayStringValue(byte[] ary, int idx, int len) {
            String v = null;
            char[] buffer = new char[ary.Length];
            char b;
            int k, n = 0;
            for (k = 0; k < len; k++) {
                b = (char)(ary[idx++] & 0xff);
                if (b == 0)
                    break;
                buffer[n++] = b;
            }
            try {
                v = new String(buffer, 0, n);
            } catch (Exception) {
                v = "";
            }
            return v;
        }

        public static byte Strlen(byte[] p) {
            byte n = 0;
            if (p != null) while (p[n] != 0) ++n;
            return n;
        }

        public static byte[] Strcpy(byte[] d, byte[] s) {
            int n = 0;
            if (d != null && s != null) {
                do {
                    d[n] = s[n];
                }
                while (s[n++] != 0);
            }
            return d;
        }

        public static int Strcmp(byte[] a, byte[] b) {
            int c, k, n, na = Strlen(a), nb = Strlen(b);
            if (na < nb) n = na; else n = nb;
            for (k = 0; k < n; ++k) {
                c = a[k] - b[k];
                if (c == 0) continue;
                return c;
            }
            if (na < nb) return 1;
            else if (na > nb) return -1;
            return 0;
        }

        public static void LogBuffer(String msg, byte[] buffer) {
            int k;
            String s = msg + ":{";
            for (k = 0; k < buffer.Length; k++) {
                s += String.Format("{0:x2}", buffer[k] & 0xff);
            }
            s += "}\n";
            Log.d(TAG, s);
        }

        public static void Interrupt(Thread t) {
            if (t != null) {
                int count = 0;
                t.Interrupt();
                ConditionVariable.Interrupt(t);
                while (t.IsAlive) {
                    try {
                        Thread.Sleep(100);
                    } catch (Exception) {
                    }
                    ++count;
                    if (count > 30) {
                        count = 0;
                        Log.w(TAG, "Interrupted Thread not stopping:" + t.Name);
                    }
                    t.Interrupt();
                    ConditionVariable.Interrupt(t);
                }
            }
        }
    }
}