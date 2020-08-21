/*!
 * https://github.com/SamsungDForum/JuvoPlayer
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
 */

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AutoFixture;
using AutoFixture.Kernel;
using JuvoPlayer.Tests.Utils;
using NUnit.Framework;

namespace JuvoPlayer.Tests.IntegrationTests
{
    [TestFixture]
    public class TSOperationSerializer
    {
        private static string SerializeOperation(TestOperation operation)
        {
            return SerializeOperations(new List<TestOperation> {operation});
        }

        private static string SerializeOperations(IEnumerable<TestOperation> operations)
        {
            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            using (var reader = new StreamReader(stream))
            {
                OperationSerializer.Serialize(writer, operations);
                stream.Position = 0;
                return reader.ReadToEnd();
            }
        }

        private static TTestOperation DeserializeOperation<TTestOperation>(string serialized)
        {
            return (TTestOperation) DeserializeOperations(serialized).Single();
        }

        private static IEnumerable<TestOperation> DeserializeOperations(string serialized)
        {
            var byteArray = Encoding.ASCII.GetBytes(serialized);
            using (var stream = new MemoryStream(byteArray))
            using (var reader = new StreamReader(stream))
                return OperationSerializer.Deserialize(reader);
        }

        [Test, TestCaseSource(nameof(TestOperations))]
        [Category("Positive")]
        public void Serialization_OperationSerialized_DeserializedProperly(TestOperation operation)
        {
            var serializedOperation = SerializeOperation(operation);
            var deserializedOperation = DeserializeOperation<TestOperation>(serializedOperation);
            Assert.That(deserializedOperation, Is.EqualTo(operation));
        }

        [Test]
        [Category("Positive")]
        public void Serialization_AllOperationsSerialized_DeserializedProperly()
        {
            var testOperations = TestOperations();
            var serializedOperations = SerializeOperations(testOperations);
            var deserializedOperations = DeserializeOperations(serializedOperations);

            Assert.That(deserializedOperations, Is.EqualTo(testOperations));
        }

        private static TestOperation[] TestOperations()
        {
            var fixture = new Fixture();
            var context = new SpecimenContext(fixture);
            var allOpTypes = AllOperations.GetAllTypes();
            return allOpTypes.Select(type => (TestOperation) context.Resolve(type)).ToArray();
        }
    }
}
