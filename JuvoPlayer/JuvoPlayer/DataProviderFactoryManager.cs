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

using JuvoPlayer.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JuvoPlayer
{
    public class DataProviderFactoryManager
    {
        private List<IDataProviderFactory> dataProviders_ = new List<IDataProviderFactory>();

        public DataProviderFactoryManager()
        {
        }

        public IDataProvider CreateDataProvider(ClipDefinition clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException("clip cannot be null");
            }

            return dataProviders_.First(o => o.SupportsClip(clip)).Create(clip);
        }

        public void RegisterDataProviderFactory(IDataProviderFactory factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException("factory cannot be null");
            }
      
            if (dataProviders_.Exists(o => o == factory))
            {
                throw new ArgumentException("factory has been already registered");
            }

            dataProviders_.Add(factory);
        }
    }
}