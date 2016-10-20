using System;
using System.Threading;

namespace Babel.Core {

    /// <summary>
    /// Wrapper for CancellationTokens.
    /// Provides a resetable Interrupt mechanism.
    /// </summary>
    public class InterruptSource {

        public CancellationTokenSource CancelTokenSource;
        public CancellationToken Token;

        public InterruptSource() {
            ResetInterrupt();
        }

        public void ResetInterrupt() {
            CancelTokenSource = new CancellationTokenSource();
            Token = CancelTokenSource.Token;
        }

        public CancellationToken GetToken {
            get {
                return Token;
            }
        }

        public void GenerateInterrupt() {
            try {
                CancelTokenSource.Cancel();
            } catch (Exception) {
            }
        }

        public bool IsInterrupted {
            get { return CancelTokenSource.IsCancellationRequested; }
        }
    }
}
