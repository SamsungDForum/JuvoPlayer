/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2020, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 */

using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using JuvoPlayer.Common;

namespace JuvoPlayer.Drms
{
    public class CdmInstanceWrapper : ICdmInstance
    {
        private readonly ICdmInstance _impl;
        private readonly ISubject<ExceptionEvent> _exceptionSubject;

        public CdmInstanceWrapper(ICdmInstance impl)
        {
            _impl = impl;
            _exceptionSubject = new Subject<ExceptionEvent>();
        }

        public void Dispose()
        {
            Intercept(() =>
                _impl.Dispose());
        }

        public string CreateSession()
        {
            return Intercept(() =>
                _impl.CreateSession());
        }

        public void GenerateRequest(string sessionId, DrmInitDataType drmInitDataType, byte[] initData)
        {
            Intercept(() =>
                _impl.GenerateRequest(
                    sessionId,
                    drmInitDataType,
                    initData));
        }

        public void UpdateSession(string sessionId, byte[] sessionData)
        {
            Intercept(() =>
                _impl.UpdateSession(
                    sessionId,
                    sessionData));
        }

        public void CloseSession(string sessionId)
        {
            Intercept(() =>
                _impl.CloseSession(sessionId));
        }

        public Packet Decrypt(EncryptedPacket packet)
        {
            return Intercept(() =>
                _impl.Decrypt(packet));
        }

        public IObservable<Message> OnSessionMessage()
        {
            return _impl.OnSessionMessage();
        }

        public IObservable<Unit> OnKeyStatusChanged()
        {
            return _impl.OnKeyStatusChanged();
        }

        public IObservable<ExceptionEvent> OnException()
        {
            return _exceptionSubject.AsObservable();
        }

        private void Intercept(Action action)
        {
            try
            {
                action.Invoke();
            }
            catch (Exception exception)
            {
                if (exception.GetType() != typeof(NoKeyException))
                    _exceptionSubject.OnNext(new ExceptionEvent(exception));
                throw;
            }
        }

        private T Intercept<T>(Func<T> func)
        {
            try
            {
                return func.Invoke();
            }
            catch (Exception exception)
            {
                if (exception.GetType() != typeof(NoKeyException))
                    _exceptionSubject.OnNext(new ExceptionEvent(exception));
                throw;
            }
        }
    }
}