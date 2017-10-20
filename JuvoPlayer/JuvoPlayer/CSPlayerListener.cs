using CSPlayer;

namespace JuvoPlayer {
    class CSPlayerListener : IPlayerEventListener {
        public void OnCurrentPosition(uint lCurrTime) {
            //throw new NotImplementedException();
        }

        public void OnEndOfStream() {
            //throw new NotImplementedException();
        }

        public void OnEnoughData(StreamType_Samsung streamType) {
            //throw new NotImplementedException();
        }

        public void OnError(PlayerErrorType_Samsung errorType, string msg) {
            //throw new NotImplementedException();
        }

        public void OnInitComplete() {
            //throw new NotImplementedException();
        }

        public void OnInitFailed() {
            // throw new NotImplementedException();
        }

        public void OnMessage(PlayerMsgType_Samsung msgType) {
            // throw new NotImplementedException();
        }

        public void OnNeedData(StreamType_Samsung streamType, uint size) {
            //throw new NotImplementedException();
        }

        public void OnSeekCompleted() {
            // throw new NotImplementedException();
        }

        public void OnSeekData(StreamType_Samsung streamType, ulong offset) {
            //throw new NotImplementedException();
        }

        public void OnSeekStartedBuffering() {
            //throw new NotImplementedException();
        }

        public void setPlayer(IPlayerAdapter playerInstance) {
            // throw new NotImplementedException();
        }
    }
}
