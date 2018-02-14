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

using System;
using System.IO;
using Newtonsoft.Json;

namespace JuvoPlayer.Common
{
    public class JSONFileReader
    {
        public static T DeserializeJsonFile<T>(string filePath)
        {
            if (filePath == null)
                throw new ArgumentNullException("filePath cannot be null");

            if (filePath.Length == 0)
                throw new ArgumentException("filePath cannot be empty");

            if (!File.Exists(filePath))
                throw new ArgumentException("json file does not exist");

            var jsonText = File.ReadAllText(filePath);
            return DeserializeJsonText<T>(jsonText);
        }

        public static T DeserializeJsonText<T>(string json)
        {
            if (json == null)
                throw new ArgumentNullException("json cannot be null");

            if (json.Length == 0)
                throw new ArgumentException("json cannot be empty");

            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
