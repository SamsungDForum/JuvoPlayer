using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace JuvoPlayer.Dash.MpdParser.Xml
{
    /// <inheritdoc />
    /// <summary>
    /// This namespace provide override System.Xml parser for MPD files.
    /// </summary>
    public class Element : System.Attribute
    {
        private readonly string _mName;
        public Element()
        {
        }
        public Element(string name)
        {
            _mName = name;
        }

        public string Name(string propName)
        {
            return _mName ?? Singular(propName);
        }

        public string RawName()
        {
            return _mName;
        }

        private static string Singular(string name)
        {
            if (name.EndsWith("xes"))
                return name.Substring(0, name.Length - 2);
            if (name.EndsWith("ies"))
                return name.Substring(0, name.Length - 3) + "y";
            if (name.EndsWith("segment"))
                return name.Substring(0, name.Length - 1);
            return name;
        }
    };

    public class Attribute : System.Attribute
    {
        private readonly string _mName;
        public Attribute()
        {
        }
        public Attribute(string name)
        {
            _mName = name;
        }

        public string Name(string propName)
        {
            return _mName ?? LowerCase(propName);
        }

        public string RawName()
        {
            return _mName;
        }

        private static string LowerCase(string name)
        {
            return name[0].ToString().ToLower() + name.Substring(1);
        }
    };

    public class InnerText : System.Attribute
    {
    }

    internal class TypeConverter
    {
        public static T[] ToArray<T>(string val, Func<string, T> conv)
        {
            var split = val.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new T[split.Length];
            for (var i = 0; i < split.Length; ++i)
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
            return XmlConvert.ToDateTime(
                val,
                XmlDateTimeSerializationMode.RoundtripKind);
        }

        public static Node.Template ToTemplate(string val)
        {
            return new Node.Template(val);
        }

        public static string Ident(string val)
        {
            return val;
        }

        public static string[] String2Array(string val)
        {
            return ToArray(val, Ident);
        }

        public static int[] String2IntArray(string val)
        {
            return ToArray(val, XmlConvert.ToInt32);
        }

        public static uint[] String2UIntArray(string val)
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
            return TypeConverter.ToInt(value);
        }

        public static uint? AsUInt(string value)
        {
            return TypeConverter.ToUInt(value);
        }

        public static long? AsLong(string value)
        {
            return TypeConverter.ToLong(value);
        }

        public static ulong? AsULong(string value)
        {
            return TypeConverter.ToULong(value);
        }

        public static double? AsDouble(string value)
        {
            return TypeConverter.ToDouble(value);
        }

        public static bool AsBool(string value)
        {
            return XmlConvert.ToBoolean(value);
        }

        public static TimeSpan? AsTimeSpan(string value)
        {
            return TypeConverter.ToTimeSpan(value);
        }

        public static DateTime? AsDateTime(string value)
        {
            return TypeConverter.ToDateTime(value);
        }

        public static Node.Template AsTemplate(string value)
        {
            return TypeConverter.ToTemplate(value);
        }

        public static string[] AsStringArray(string value)
        {
            return TypeConverter.String2Array(value);
        }

        public static int[] AsIntArray(string value)
        {
            return TypeConverter.String2IntArray(value);
        }

        public static uint[] AsUIntArray(string value)
        {
            return TypeConverter.String2UIntArray(value);
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
            throw new ArgumentException(
                string.Format("Type of value does not match.", type));
        }

        internal static object ArrayFrom(string value, Type type)
        {
            if (type == typeof(string)) return AsStringArray(value);
            if (type == typeof(int)) return AsIntArray(value);
            if (type == typeof(uint)) return AsUIntArray(value);
            throw new ArgumentException(
                string.Format("Type of value does not match.", type));
        }

        internal static object ObjectFrom(string value, Type type)
        {
            if (type == typeof(string)) return AsString(value);
            if (type == typeof(Node.Template)) return AsTemplate(value);
            throw new ArgumentException(
                string.Format("Type of value does not match.", type));
        }
    }

    internal class Parser
    {
        private static string InnerText(XmlReader reader)
        {
            var s = string.Empty;
            var depth = 1;
            while (reader.Read())
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
                        s += reader.Value;
                        break;
                    case XmlNodeType.Attribute:
                        break;
                    case XmlNodeType.CDATA:
                        break;
                    case XmlNodeType.Comment:
                        break;
                    case XmlNodeType.Document:
                        break;
                    case XmlNodeType.DocumentFragment:
                        break;
                    case XmlNodeType.DocumentType:
                        break;
                    case XmlNodeType.EndEntity:
                        break;
                    case XmlNodeType.Entity:
                        break;
                    case XmlNodeType.EntityReference:
                        break;
                    case XmlNodeType.None:
                        break;
                    case XmlNodeType.Notation:
                        break;
                    case XmlNodeType.ProcessingInstruction:
                        break;
                    case XmlNodeType.SignificantWhitespace:
                        break;
                    case XmlNodeType.Whitespace:
                        break;
                    case XmlNodeType.XmlDeclaration:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                if (depth == 0)
                    break;
            }
            return s;
        }

        private static void Ignore(XmlReader reader)
        {
            var depth = 1;
            while (reader.Read())
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
                    case XmlNodeType.Attribute:
                        break;
                    case XmlNodeType.CDATA:
                        break;
                    case XmlNodeType.Comment:
                        break;
                    case XmlNodeType.Document:
                        break;
                    case XmlNodeType.DocumentFragment:
                        break;
                    case XmlNodeType.DocumentType:
                        break;
                    case XmlNodeType.EndEntity:
                        break;
                    case XmlNodeType.Entity:
                        break;
                    case XmlNodeType.EntityReference:
                        break;
                    case XmlNodeType.None:
                        break;
                    case XmlNodeType.Notation:
                        break;
                    case XmlNodeType.ProcessingInstruction:
                        break;
                    case XmlNodeType.SignificantWhitespace:
                        break;
                    case XmlNodeType.Whitespace:
                        break;
                    case XmlNodeType.XmlDeclaration:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                if (depth == 0)
                    break;
            }
        }

        private static void Attributes<T>(XmlReader reader, T result)
        {
            var attrs = new Dictionary<string, PropertyInfo>();
            var type = result.GetType();
            foreach (var property in type.GetProperties())
            {
                foreach (var attr in property.GetCustomAttributes(false))
                {
                    if (!(attr is Attribute))
                        continue;
                    var xmlname = ((Attribute)attr).Name(property.Name);
                    attrs.Add(xmlname, property);
                }
            }

            var attcount = reader.AttributeCount;
            for (var i = 0; i < attcount; i++)
            {
                reader.MoveToAttribute(i);
                if (!attrs.TryGetValue(reader.Name, out var property))
                    continue;

                var propType = property.PropertyType;
                if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    Node.Internal.SetValue(
                        property,
                        result,
                        Attr.NullableFrom(reader.Value, Nullable.GetUnderlyingType(propType)));
                }
                else if (propType.IsArray)
                {
                    Node.Internal.SetValue(
                        property,
                        result,
                        Attr.ArrayFrom(reader.Value, propType.GetElementType()));
                }
                else if (propType == typeof(bool))
                {
                    Node.Internal.SetValue(
                        property,
                        result,
                        Attr.AsBool(reader.Value));
                }
                else
                {
                    Node.Internal.SetValue(
                        property,
                        result,
                        Attr.ObjectFrom(reader.Value, propType));
                }
            }
            reader.MoveToElement();
        }

        private class Property {
            public readonly Type Type;
            private readonly PropertyInfo _property;
            private object _container;
            private MethodInfo _add;
            private MethodInfo _toArray;

            public Property(PropertyInfo property)
            {
                Type = property.PropertyType.GetElementType();
                _property = property;
                _container = null;
                _add = null;
                _toArray = null;
            }

            public void Add(object item)
            {
                if (_container == null)
                {
                    var listType = typeof(List<>).MakeGenericType(Type);
                    var ci = listType.GetConstructor(new Type[] { });
                    _container = ci.Invoke(new object[] { });
                    _add = listType.GetMethod("Add", new[] { Type });
                    _toArray = listType.GetMethod("ToArray");
                    var info = listType.GetProperty("Count");
                    info.GetGetMethod();
                }
                _add.Invoke(_container, new[] { item });
            }

            public void Store<T>(T result)
            {
                var value = _container == null ?
                    Activator.CreateInstance(Type.MakeArrayType(), 0) :
                    _toArray.Invoke(_container, new object[] { });

                Node.Internal.SetValue(_property, result, value);
            }
        }

        private static void Children<T>(XmlReader reader, T result)
        {
            var elems = new Dictionary<string, Property>();

            var type = result.GetType();
            foreach (var property in type.GetProperties())
            {
                foreach (var attr in property.GetCustomAttributes(false))
                {
                    switch (attr)
                    {
                        case Element element:
                            elems.Add(
                                element.Name(property.Name),
                                new Property(property));
                            break;
                        case InnerText _:
                            Node.Internal.SetValue(
                                property,
                                result,
                                InnerText(reader));
                            return;
                    }
                }
            }

            if (reader.IsEmptyElement)
            {
                reader.Read();
                foreach (var pair in elems)
                    pair.Value.Store(result);
                return;
            }

            var gotOut = false;
            while (!gotOut && reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (!elems.ContainsKey(reader.Name))
                        {
                            Ignore(reader);
                            continue;
                        }
                        var name = reader.Name;
                        var property = elems[name];
                        elems[name].Add(
                            CreateAndRead(property.Type, reader, result));
                        if (!reader.IsEmptyElement)
                            break;
                        goto case XmlNodeType.EndElement;

                    case XmlNodeType.EndElement:
                        gotOut = true;
                        break;
                    case XmlNodeType.Attribute:
                        break;
                    case XmlNodeType.CDATA:
                        break;
                    case XmlNodeType.Comment:
                        break;
                    case XmlNodeType.Document:
                        break;
                    case XmlNodeType.DocumentFragment:
                        break;
                    case XmlNodeType.DocumentType:
                        break;
                    case XmlNodeType.EndEntity:
                        break;
                    case XmlNodeType.Entity:
                        break;
                    case XmlNodeType.EntityReference:
                        break;
                    case XmlNodeType.None:
                        break;
                    case XmlNodeType.Notation:
                        break;
                    case XmlNodeType.ProcessingInstruction:
                        break;
                    case XmlNodeType.SignificantWhitespace:
                        break;
                    case XmlNodeType.Text:
                        break;
                    case XmlNodeType.Whitespace:
                        break;
                    case XmlNodeType.XmlDeclaration:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            foreach(var pair in elems)
                pair.Value.Store(result);
        }

        private static object Create<T>(Type type, T parent)
        {
            ConstructorInfo c = type.GetConstructor(new[] { parent.GetType() });
            if (c != null)
                return c.Invoke(new object[] { parent });

            return Activator.CreateInstance(type);
        }

        private static object CreateAndRead<T>(Type type, XmlReader reader, T parent)
        {
            if (type == typeof(string))
                return InnerText(reader);

            var o = Create(type, parent);
            Attributes(reader, o);
            Children(reader, o);
            return o;
        }

        public static void Parse<T>(TextReader io, T result, string name)
        {
            var reader = XmlReader.Create(io);
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (reader.Name != name)
                        {
                            Ignore(reader);
                            break;
                        }
                        Attributes(reader, result);
                        Children(reader, result);
                        return;
                    case XmlNodeType.Attribute:
                        break;
                    case XmlNodeType.CDATA:
                        break;
                    case XmlNodeType.Comment:
                        break;
                    case XmlNodeType.Document:
                        break;
                    case XmlNodeType.DocumentFragment:
                        break;
                    case XmlNodeType.DocumentType:
                        break;
                    case XmlNodeType.EndElement:
                        break;
                    case XmlNodeType.EndEntity:
                        break;
                    case XmlNodeType.Entity:
                        break;
                    case XmlNodeType.EntityReference:
                        break;
                    case XmlNodeType.None:
                        break;
                    case XmlNodeType.Notation:
                        break;
                    case XmlNodeType.ProcessingInstruction:
                        break;
                    case XmlNodeType.SignificantWhitespace:
                        break;
                    case XmlNodeType.Text:
                        break;
                    case XmlNodeType.Whitespace:
                        break;
                    case XmlNodeType.XmlDeclaration:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}
