using System;

namespace Babel.BabelProtocol {

    public class AddressParams {
        public ushort Receiver;
        public ushort Sender;
        public byte SenderId;
        public byte flagsRS;

        public AddressParams() {
        }
    }
}