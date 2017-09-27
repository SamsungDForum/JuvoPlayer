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


using System.Threading;

namespace JuvoPlayer.RTSP
{
    public interface ISharedBuffer
    {
        void ClearData();
        byte[] ReadData(int size); // it should remove leading [size] bytes of data that it returns
        void WriteData(byte[] data);

        WaitHandle GetWaitHandle(); // the wait handle should be set when new data is received or end of stream is reached
        int StoredDataSize(); // return current size of stored data
        bool EndOfData(); // return true if no more data will be available or false otherwise
    }
}