/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using MpdParser.Node;
namespace MpdParser.Xml
{
    public class Element : System.Attribute
    {
        private readonly string mName;

        public Element()
        {
        }

        public Element(string name)
        {
            mName = name;
        }

        public string Name(string propName)
        {
            return mName ?? Singular(propName);
        }

        public string RawName()
        {
            return mName;
        }

        private static string Singular(string name)
        {
            if (name.EndsWith("xes"))
                return name.Substring(0, name.Length - 2);
            if (name.EndsWith("ies"))
                return name.Substring(0, name.Length - 3) + "y";
            if (name.EndsWith("s"))
                return name.Substring(0, name.Length - 1);
            return name;
        }
    }

    public class Attribute : System.Attribute
    {
        private string mName;

        public Attribute()
        {
        }

        public Attribute(string name)
        {
            mName = name;
        }

        public string Name(string propName)
        {
            return mName ?? LowerCase(propName);
        }

        public string RawName()
        {
            return mName;
        }

        private static string LowerCase(string name)
        {
            return name[0].ToString().ToLower() + name.Substring(1);
        }
    }

    public class InnerXml : System.Attribute
    {
    }

    public class InnerText : System.Attribute
    {
    }

    internal class Conv
    {
        public static T[] ToArray<T>(string val, Func<string, T> conv)
        {
            string[] split = val.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            T[] result = new T[split.Length];
            for (int i = 0; i < split.Length; ++i)
                result[i] = conv(split[i]);
            return result;
        }

        public static int? ToInt(string val)
        {
            return XmlConvert.ToInt32(val);
        }

        public static uint? ToUInt(string val)
        {
            return XmlConvert.ToUInt32(val);
        }

        public static long? ToLong(string val)
        {
            return XmlConvert.ToInt64(val);
        }

        public static ulong? ToULong(string val)
        {
            return XmlConvert.ToUInt64(val);
        }

        public static double? ToDouble(string val)
        {
            return XmlConvert.ToDouble(val);
        }

        public static TimeSpan? ToTimeSpan(string val)
        {
            return XmlConvert.ToTimeSpan(val);
        }

        public static DateTime? ToDateTime(string val)
        {
            return XmlConvert.ToDateTime(val,
                XmlDateTimeSerializationMode.RoundtripKind);
        }

        public static Template ToTemplate(string val)
        {
            return new Template(val);
        }

        public static string Ident(string val)
        {
            return val;
        }

        public static string[] A2s(string val)
        {
            return ToArray(val, Ident);
        }

        public static int[] A2int(string val)
        {
            return ToArray(val, XmlConvert.ToInt32);
        }

        public static uint[] A2uint(string val)
        {
            return ToArray(val, XmlConvert.ToUInt32);
        }
    }

    internal class Attr
    {
        public static string AsString(string value)
        {
            return value;
        }

        public static int? AsInt(string value)
        {
            return Conv.ToInt(value);
        }

        public static uint? AsUInt(string value)
        {
            return Conv.ToUInt(value);
        }

        public static long? AsLong(string value)
        {
            return Conv.ToLong(value);
        }

        public static ulong? AsULong(string value)
        {
            return Conv.ToULong(value);
        }

        public static double? AsDouble(string value)
        {
            return Conv.ToDouble(value);
        }

        public static bool AsBool(string value)
        {
            return XmlConvert.ToBoolean(value);
        }

        public static TimeSpan? AsTimeSpan(string value)
        {
            return Conv.ToTimeSpan(value);
        }

        public static DateTime? AsDateTime(string value)
        {
            return Conv.ToDateTime(value);
        }

        public static Template AsTemplate(string value)
        {
            return Conv.ToTemplate(value);
        }

        public static string[] AsStringArray(string value)
        {
            return Conv.A2s(value);
        }

        public static int[] AsIntArray(string value)
        {
            return Conv.A2int(value);
        }

        public static uint[] AsUIntArray(string value)
        {
            return Conv.A2uint(value);
        }

        internal static object NullableFrom(string value, Type type)
        {
            if (type == typeof(int)) return AsInt(value);
            if (type == typeof(uint)) return AsUInt(value);
            if (type == typeof(long)) return AsLong(value);
            if (type == typeof(ulong)) return AsULong(value);
            if (type == typeof(double)) return AsString(value);
            if (type == typeof(TimeSpan)) return AsTimeSpan(value);
            if (type == typeof(DateTime)) return AsDateTime(value);
            throw new NotImplementedException();
        }

        internal static object ArrayFrom(string value, Type type)
        {
            if (type == typeof(string)) return AsStringArray(value);
            if (type == typeof(int)) return AsIntArray(value);
            if (type == typeof(uint)) return AsUIntArray(value);
            throw new NotImplementedException();
        }

        internal static object ObjectFrom(string value, Type type)
        {
            if (type == typeof(string)) return AsString(value);
            if (type == typeof(Template)) return AsTemplate(value);
            throw new NotImplementedException();
        }
    }

    internal class Parser
    {
        private static async Task<string> InnerText(XmlReader reader)
        {
            var sb = new StringBuilder();
            int depth = 1;
            while (await reader.ReadAsync())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        depth += 1;
                        if (!reader.IsEmptyElement)
                            break;
                        goto case XmlNodeType.EndElement;

                    case XmlNodeType.EndElement:
                        depth -= 1;
                        break;

                    case XmlNodeType.Text:
                        sb.Append(reader.Value);
                        break;
                }

                if (depth == 0)
                    break;
            }

            return sb.ToString();
        }

        private static async Task IgnoreAsync(XmlReader reader)
        {
            int depth = 1;
            while (await reader.ReadAsync())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        depth += 1;
                        if (!reader.IsEmptyElement)
                            break;
                        goto case XmlNodeType.EndElement;

                    case XmlNodeType.EndElement:
                        depth -= 1;
                        break;

                    case XmlNodeType.Text:
                        break;
                }

                if (depth == 0)
                    break;
            }
        }

        private static void Attributes<T>(XmlReader reader, T result)
        {
            Dictionary<string, PropertyInfo> attrs = new Dictionary<string, PropertyInfo>();
            Type type = result.GetType();
            foreach (PropertyInfo prop in type.GetProperties())
            {
                foreach (Object attr in prop.GetCustomAttributes(false))
                {
                    if (!(attr is Attribute))
                        continue;
                    string xmlname = ((Attribute)attr).Name(prop.Name);
                    attrs.Add(xmlname, prop);
                }
            }

            int attcount = reader.AttributeCount;
            for (int i = 0; i < attcount; i++)
            {
                reader.MoveToAttribute(i);
                if (!attrs.TryGetValue(reader.Name, out PropertyInfo prop))
                    continue;

                Type propType = prop.PropertyType;
                if (propType.IsGenericType && propType.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
                {
                    Internal.SetValue(prop, result,
                        Attr.NullableFrom(reader.Value, Nullable.GetUnderlyingType(propType)));
                }
                else if (propType.IsArray)
                {
                    Internal.SetValue(prop, result, Attr.ArrayFrom(reader.Value, propType.GetElementType()));
                }
                else if (propType == typeof(bool))
                {
                    Internal.SetValue(prop, result, Attr.AsBool(reader.Value));
                }
                else
                {
                    Internal.SetValue(prop, result, Attr.ObjectFrom(reader.Value, propType));
                }
            }

            reader.MoveToElement();
        }

        private class Property
        {
            public PropertyInfo prop;
            public Type type;
            public object container;
            public MethodInfo add;
            public MethodInfo toArray;
            public MethodInfo count;

            public Property(PropertyInfo prop)
            {
                this.prop = prop;
                type = prop.PropertyType.GetElementType();
                container = null;
                add = null;
                toArray = null;
                count = null;
            }

            public void Add(object item)
            {
                if (container == null)
                {
                    Type listType = typeof(List<>).MakeGenericType(type);
                    ConstructorInfo ci = listType.GetConstructor(new Type[] { });
                    container = ci.Invoke(new object[] { });
                    add = listType.GetMethod("Add", new[] { type });
                    toArray = listType.GetMethod("ToArray");
                    PropertyInfo info = listType.GetProperty("Count");
                    count = info.GetGetMethod();
                }

                add.Invoke(container, new[] { item });
            }

            public void Store<T>(T result)
            {
                var value = container == null
                    ? Activator.CreateInstance(type.MakeArrayType(), 0)
                    : toArray.Invoke(container, new object[] { });

                Internal.SetValue(prop, result, value);
            }
        }

        //
        /// <summary>
        /// Method parses all subchildred at given note.
        /// Entry point (reader state). If must be @beginning of non end node with
        /// all attributes processed.
        /// </summary>
        /// <param name="reader">System.Xml.XmlReader processing current document</param>
        /// <param name="result">Template object which will collected parsed data</param>
        /// <param name="parent">String containing name of the parent object. Return to higher
        /// will only be performed for a given node if </current node> tag is found</param>
        /// <returns>
        /// bool
        /// true -  rescan at same level as current operation gobbled up
        ///         all end tags.
        /// false - continue on current level looking for matching </end tag>
        /// </returns>
        /// <remarks>
        /// Return value is a "hack" to work around internal behaviour of certain
        /// XMLReader methods which gobble up end tags.
        /// </remarks>
        ///
        private static async Task<bool> Children<T>(XmlReader reader, T result, string parent = null)
        {
            Dictionary<string, Property> elems = new Dictionary<string, Property>();

            Type type = result.GetType();
            foreach (PropertyInfo prop in type.GetProperties())
            {
                foreach (Object attr in prop.GetCustomAttributes(false))
                {
                    if (attr is Element element)
                    {
                        elems.Add(element.Name(prop.Name), new Property(prop));
                    }
                    else if (attr is InnerText)
                    {
                        // Note:
                        // Behaviour of exit (false/true) may require adjusting,
                        // depending where reader will be after InnerText() exits.
                        //
                        Internal.SetValue(prop, result, await InnerText(reader));
                        return false;
                    }
                    else if (attr is InnerXml)
                    {
                        // Read inner XML gobbles up all </> token up to next
                        // element. As such, inform caller about this so continuation
                        // on current level is performed
                        //

                        // "cleanup" of retrieved data is necessary in order to compare
                        // content protection data. ReadInnerXML() will read data with any white chars
                        // in between, while system reader (used in unit tests for comparison purposes)
                        // cleans internal data (due to different internal representation)
                        string[] tmpdata = (await reader.ReadInnerXmlAsync()).Split('\n', '\r', '\t');

                        for (int i = 0; i < tmpdata.Length; i++)
                        {
                            tmpdata[i] = tmpdata[i].Trim('\n', '\r', '\t', ' ');
                        }

                        string tmp = String.Join("", tmpdata);
                        Internal.SetValue(prop, result, $"<xml>{tmp}</xml>");
                        return true;
                    }
                }
            }

            //
            // We will never be here. Children are not processed
            // if parent was single lined entry
            // i.e. <element ..... />
            //
            /*
            if (reader.IsEmptyElement)
            {
                reader.Read();
                foreach (KeyValuePair<string, Property> pair in elems)
                    pair.Value.Store(result);
                return;
            }
            */

            bool gotOut = false;
            bool rescan = false;
            while (!gotOut)
            {
                if (rescan == false)
                {
                    if (await reader.ReadAsync() == false)
                    {
                        gotOut = true;
                        continue;
                    }
                }
                else
                {
                    //Reset rescan flag. It is only needed "once"
                    rescan = false;
                }

                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (!elems.ContainsKey(reader.Name))
                        {
                            continue;
                        }

                        string name = reader.Name;
                        Property prop = elems[name];
                        object o;

                        (o, rescan) = await CreateAndRead(prop.type, reader, result, name);
                        elems[name].Add(o);
                        if (rescan)
                        {
                            break;
                        }

                        if (reader.NodeType == XmlNodeType.EndElement)
                        {
                            goto case XmlNodeType.EndElement;
                        }

                        break;

                    case XmlNodeType.EndElement:
                        //
                        // Exit to an upper level ONLY if end element
                        // is a "matching" one.
                        // i.e. When processing node <X>, end element is </X>
                        // otherwise keep on processing all stuff inside current
                        // node without bailing up.
                        //
                        if (parent == reader.Name)
                        {
                            gotOut = true;
                        }

                        break;
                }
            }

            foreach (var pair in elems)
                pair.Value.Store(result);

            return false;
        }

        private static object Create<T>(Type type, T parent)
        {
            ConstructorInfo c = type.GetConstructor(new[] { parent.GetType() });
            if (c != null)
                return c.Invoke(new object[] { parent });

            return Activator.CreateInstance(type);
        }

        private static async Task<(object, bool)> CreateAndRead<T>(Type type, XmlReader reader, T parent, string name = null)
        {
            object o;
            bool rescanLevel = false;
            if (type == typeof(string))
            {
                o = await InnerText(reader);
            }
            else
            {
                o = Create(type, parent);

                Attributes(reader, o);
                if (!reader.IsEmptyElement)
                {
                    rescanLevel = await Children(reader, o, name);
                }
            }

            return (o, rescanLevel);
        }

        public static async Task ParseAsync<T>(TextReader io, T result, string name)
        {
            var reader = XmlReader.Create(io, new XmlReaderSettings { Async = true });
            while (await reader.ReadAsync())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (reader.Name != name)
                        {
                            await IgnoreAsync(reader);
                            break;
                        }

                        Attributes(reader, result);
                        if (!reader.IsEmptyElement)
                        {
                            await Children(reader, result, name);
                        }

                        return;
                }
            }
        }
    }
}