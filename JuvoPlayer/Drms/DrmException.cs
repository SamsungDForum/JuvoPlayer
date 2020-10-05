/*!
 * https://github.com/SamsungDForum/JuvoPlayer
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
 */

using System;

namespace JuvoPlayer.Drms
{
    public class DrmException : Exception
    {
        public DrmException()
        {
        }

        public DrmException(string message) : base(message)
        {
        }

        public DrmException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class DecryptException : DrmException
    {
        public DecryptException()
        {
        }

        public DecryptException(string message) : base(message)
        {
        }

        public DecryptException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class NoKeyException : DecryptException
    {
        public NoKeyException()
        {
        }

        public NoKeyException(string message) : base(message)
        {
        }

        public NoKeyException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class DecryptBufferFullException : DecryptException
    {
        public DecryptBufferFullException()
        {
        }

        public DecryptBufferFullException(string message) : base(message)
        {
        }

        public DecryptBufferFullException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}