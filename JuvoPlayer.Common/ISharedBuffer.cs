// Copyright (c) 2017 Samsung Electronics Co., Ltd All Rights Reserved
// PROPRIETARY/CONFIDENTIAL 
// This software is the confidential and proprietary
// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall
// not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into with
// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the
// suitability of the software, either express or implied, including but not
// limited to the implied warranties of merchantability, fitness for a
// particular purpose, or non-infringement. SAMSUNG shall not be liable for any
// damages suffered by licensee as a result of using, modifying or distributing
// this software or its derivatives.


namespace JuvoPlayer.Common
{
    public interface ISharedBuffer
    {
        void ClearData();

        // SharedBuffer::ReadData(int size) is blocking - it will block until it has enough data or return less data if EOF is reached.
        // Returns byte array of leading [size] bytes of data from the buffer; it should remove the leading [size] bytes of data from the buffer.
        byte[] ReadData(int size);
        
        void WriteData(byte[] data, bool endOfData = false);

    }
}