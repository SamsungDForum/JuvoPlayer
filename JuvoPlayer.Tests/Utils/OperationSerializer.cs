/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2019, Samsung Electronics Co., Ltd
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
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace JuvoPlayer.Tests.Utils
{
    public static class OperationSerializer
    {
        [Serializable]
        public class SerializedOperation
        {
            public string TypeName { get; set; }
            public string Serialized { get; set; }
        }

        public static void Serialize(StreamWriter writer, IEnumerable<TestOperation> operations)
        {
            var serializedOperations = CreateSerializedOperations(operations);

            var serializer = new XmlSerializer(typeof(List<SerializedOperation>));
            serializer.Serialize(writer, serializedOperations);
        }

        private static List<SerializedOperation> CreateSerializedOperations(IEnumerable<TestOperation> operations)
        {
            var serializedOperations = new List<SerializedOperation>();

            foreach (var operation in operations)
            {
                var serializer = new XmlSerializer(operation.GetType());
                using (var writer = new StringWriter())
                {
                    serializer.Serialize(writer, operation);

                    var serializedOperation = new SerializedOperation
                    {
                        TypeName = operation.GetType().FullName,
                        Serialized = writer.ToString()
                    };

                    serializedOperations.Add(serializedOperation);
                }
            }

            return serializedOperations;
        }

        public static IList<TestOperation> Deserialize(StreamReader reader)
        {
            var serializedOperations = DeserializeSerializedOperations(reader);

            var operations = new List<TestOperation>();

            foreach (var serializedOperation in serializedOperations)
            {
                var serializer = new XmlSerializer(Type.GetType(serializedOperation.TypeName));
                using (var opReader = new StringReader(serializedOperation.Serialized))
                {
                    var operation = (TestOperation) serializer.Deserialize(opReader);
                    operations.Add(operation);
                }
            }

            return operations;
        }

        private static IEnumerable<SerializedOperation> DeserializeSerializedOperations(StreamReader reader)
        {
            var serializer = new XmlSerializer(typeof(List<SerializedOperation>));
            return (List<SerializedOperation>) serializer.Deserialize(reader);
        }
    }
}