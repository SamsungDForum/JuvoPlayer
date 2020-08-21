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

﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using MpdParser.Node;
using MpdParser.Node.Writers;

namespace JuvoPlayer.Tests.UnitTests
{
    public static class DASHConverterHelper
    {
        public static void FillProperties<T, Tbase>(this T target, Tbase baseInstance)
        where T : Tbase
        {
            Type t = typeof(T);
            Type tb = typeof(Tbase);
            PropertyInfo[] properties = tb.GetProperties();
            foreach (PropertyInfo pi in properties)
            {
                // Read value
                object value = pi.GetValue(baseInstance, null);

                // Get Property of target class
                PropertyInfo pi_target = t.GetProperty(pi.Name);

                // Write value to target
                pi_target.SetValue(target, value, null);
            }
        }

        public static bool IsNullable<T>(T obj)
        {
            if (!typeof(T).IsGenericType)
                return false;

            return typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static object GetDefaultValue(this Type t)
        {
            if (t != null && t.IsValueType && Nullable.GetUnderlyingType(t) == null && t.IsPrimitive)
                return Activator.CreateInstance(t);
            else
                return null;
        }

        public static bool IsSetToDefault(this Type ObjectType, object ObjectValue=null)
        {
            // If no ObjectType was supplied, attempt to determine from ObjectValue
            if (ObjectType == null)
            {
                // If no ObjectValue was supplied... bye bye
                if (ObjectValue == null)
                {
                    return false;
                }

                // Determine ObjectType from ObjectValue
                ObjectType = ObjectValue.GetType();
            }

            // Get the default value of type ObjectType
            object Default = ObjectType.GetDefaultValue();

            // If a non-null ObjectValue was supplied, compare Value with its default value and return the result
            if (ObjectValue != null)
                return ObjectValue.Equals(Default);

            // Since a null ObjectValue was supplied, report whether its default value is null
            return Default == null;
        }


    }


    /// <remarks>
    ///  While it may be tempting to run converter in parallel (with anything else)
    ///  this is not possible right now due to way currDoc is passed down to Period/AdaptationSet/etc.
    ///  creation
    /// </remarks>
    public class DASHConverter
    {
        // This is bit of a hack to work around the following problem:
        // Period have only a getter of internal field Document. As such
        // it can only be set at Period Creation. Simple passing of args to converter
        // method was not found so use this approach for now...
        //
        protected static DASH currDoc;
        protected static Period currPeriod;
        protected static AdaptationSet currAdaptationSet;

        // Dictionary for storing
        // "special case comparison functions" - where normal comparison
        // fails short. i.e. Xmlns is an example. Upper/Lower casing.
        //
        protected static Dictionary<string,
            Func<object[], object[],string, string, PropertyInfo, Type, Type, bool>> CustomComparers =
          new Dictionary<string, Func<object[], object[], string, string, PropertyInfo, Type, Type, bool>>()
          {
              {"Xmlns", new  Func<object[], object[], string, string, PropertyInfo, Type, Type, bool>(CompareXmlns) }
              ,{"StartWithSAP", new  Func<object[], object[], string, string, PropertyInfo, Type, Type, bool>(CompareXXXWithSAP) }
              ,{"SubsegmentStartsWithSAP", new  Func<object[], object[], string, string, PropertyInfo, Type, Type, bool>(CompareXXXWithSAP) }
              ,{"NumChannels", new  Func<object[], object[], string, string, PropertyInfo, Type, Type, bool>(CompareObsoleteAttrib) }
              ,{"SampleRate", new  Func<object[], object[], string, string, PropertyInfo, Type, Type, bool>(CompareObsoleteAttrib) }
            //  ,{"Ss", new  Func<object[], object[], string, string, PropertyInfo, Type, Type, bool>(CompareSs) }

          };

        protected static bool CompareSs(object[] a, object[] b, string adesc, string bdesc,
           PropertyInfo property, Type ta, Type tb)
        {
            if (a.Length != a.Length)
            {
                // Panic only if array is not completely empty
                bool allNull = true;
                allNull &= Array.TrueForAll(a, v => { return (v == null); });
                allNull &= Array.TrueForAll(b, v => { return (v == null); });
                if (allNull)
                    return true;

                System.Diagnostics.Debug.WriteLine($"ERROR: Array length mismatch {property.Name}");
                System.Diagnostics.Debug.WriteLine($"     {adesc} = '{a[0]}' {ta.Namespace}.{ta.Name}");
                System.Diagnostics.Debug.WriteLine($"     {bdesc} = '{b[0]}' {tb.Namespace}.{tb.Name}");
            }

            var ass = a as S[];
            var bss = b as S[];

            bool res = true;
            for(int i=0;i<ass.Length;i++)
            {
                if (Equals(ass[i].D, bss[i].D))
                    continue;

                System.Diagnostics.Debug.WriteLine($"Information: Obsolete property by current standard {property.Name}");
                System.Diagnostics.Debug.WriteLine($"     {adesc} = '{a[0]}' {ta.Namespace}.{ta.Name}");
                System.Diagnostics.Debug.WriteLine($"     {bdesc} = '{b[0]}' {tb.Namespace}.{tb.Name}");

                res = false;
            }

            return res;
        }

        protected static bool CompareObsoleteAttrib(object[] a, object[] b, string adesc, string bdesc,
           PropertyInfo property, Type ta, Type tb)
        {
            if(a.Length != b.Length)
            {
                System.Diagnostics.Debug.WriteLine($"Information: Obsolete property by current standard {property.Name}. Containing array length mismatch");
                System.Diagnostics.Debug.WriteLine($"     {adesc} Length = '{a.Length}' {ta.Namespace}.{ta.Name}");
                System.Diagnostics.Debug.WriteLine($"     {bdesc} Length = '{b.Length}' {tb.Namespace}.{tb.Name}");
                return true;
            }


            for (int i = 0; i < a.Length; i++)
            {
                if (Equals(a[i], b[i]))
                    continue;

                System.Diagnostics.Debug.WriteLine($"Information: Obsolete property by current standard {property.Name}");
                System.Diagnostics.Debug.WriteLine($"     {adesc} = '{a[0]}' {ta.Namespace}.{ta.Name}");
                System.Diagnostics.Debug.WriteLine($"     {bdesc} = '{b[0]}' {tb.Namespace}.{tb.Name}");
            }

            return true;
        }

        //MpdParser.Node.Representation.SampleRate
        protected static bool CompareXmlns(object[] a, object[] b, string adesc, string bdesc,
            PropertyInfo property, Type ta, Type tb)
        {
            bool res = true;

            for (int i = 0; i < a.Length; i++)
            {
                int comp = String.Compare((string)a[i], (string)b[i], false);

                if (comp == 0)
                    continue;

                comp = String.Compare((string)a[i], (string)b[i], true);

                if ( comp == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Case mismatch {property.Name}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: Data Mismatch! {property.Name}");
                    res &= false;
                }
                System.Diagnostics.Debug.WriteLine($"     {adesc} = '{a[i]}' {ta.Namespace}.{ta.Name}");
                System.Diagnostics.Debug.WriteLine($"     {bdesc} = '{b[i]}' {tb.Namespace}.{tb.Name}");

            }

            return res;
        }
        protected static bool CompareXXXWithSAP(object[] a, object[] b, string adesc, string bdesc,
            PropertyInfo property, Type ta, Type tb)
        {

            if (a[0] == a[0])
                return true;


            // AdaptationSet / RepresentationBase field
            // SubsegmentStartsWithSAP / StartWithSAP come with "default 0" value from
            // system parser. So if we have a combo 0 / NULL, treat this as OK
            bool isok = true;
            isok &= ((int?)a[0] == null || (int?)a[0] == 0);
            isok &= ((int?)b[0] == null || (int?)b[0] == 0);

            if (isok)
                return true;

            System.Diagnostics.Debug.WriteLine($"ERROR: Data Mismatch! {property.Name}");
            System.Diagnostics.Debug.WriteLine($"     {adesc} = '{a[0]}' {ta.Namespace}.{ta.Name}");
            System.Diagnostics.Debug.WriteLine($"     {bdesc} = '{b[0]}' {tb.Namespace}.{tb.Name}");

            return false;
        }
        protected static bool CompareGeneric(object a, object b, string adesc, string bdesc,
            PropertyInfo property, Type ta, Type tb)
        {
            if (a == null && b == null)
                return true;

            bool result = true;

            // Check for null/non null mismatch
            result &= (a != null);
            result &= (b != null);

            if(result == false)
            {
                // For "nullable" types, check Null / Default value combo. Such cases can be observed
                // as in-app XML parser will "skip" certain fields, while system parser may default
                // some attributes which are not present in MPD XML.
                // such combo is valid (info will be shown).
                if( a != null)
                {
                    result = DASHConverterHelper.IsSetToDefault(a?.GetType(), a);
                }
                else
                {
                    result &= DASHConverterHelper.IsSetToDefault(b?.GetType(), b);
                }

                if (result)
                {
                    System.Diagnostics.Debug.WriteLine($"Information: Null / Default(T) combination is treated as valid {property.Name}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: Data Mismatch Null/Non Null! {property.Name}");
                }

                System.Diagnostics.Debug.WriteLine($"     {adesc} = '{a}' {ta.Namespace}.{ta.Name}");
                System.Diagnostics.Debug.WriteLine($"     {bdesc} = '{b}' {tb.Namespace}.{tb.Name}");

                return result;
            }

            // Check if underlying type is array.
            // Equals does not check array content.
            bool isArrayA = a.GetType().IsArray;
            bool isArrayB = b.GetType().IsArray;

            // Check for Array/Non Array mismatch
            if(isArrayA != isArrayB)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: Data Storage Mismatch Array/Non Array! {property.Name}");
                System.Diagnostics.Debug.WriteLine($"     {adesc} = '{a}' {ta.Namespace}.{ta.Name}");
                System.Diagnostics.Debug.WriteLine($"     {bdesc} = '{b}' {tb.Namespace}.{tb.Name}");
                return false;
            }

            bool res = true;

            if (!isArrayA && !isArrayB)
            {
                // Non Array case - can be compared "as is"
                res = Equals(a, b);

                if (!res)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: Data Mismatch! {property.Name}");
                    System.Diagnostics.Debug.WriteLine($"     {adesc} = '{a}' {ta.Namespace}.{ta.Name}");
                    System.Diagnostics.Debug.WriteLine($"     {bdesc} = '{b}' {tb.Namespace}.{tb.Name}");
                    return false;
                }
            }
            else
            {
                //Array case. We'll need to check all elements individually
                //Do so via recursion to this function in case of
                //inception - arrays inside arrays...
                object[] arrayA = (object[])a;
                object[] arrayB = (object[])b;

                // But first check array sizes
                if(arrayA.Length != arrayB.Length)
                {
                    // Do not panic just yet. If arrays are full of nulls - we do not care
                    var arrayAContainsOnlyNulls = Array.TrueForAll(arrayA, v => v == null);
                    var arrayBContainsOnlyNulls = Array.TrueForAll(arrayB, v => v == null);

                    if (arrayAContainsOnlyNulls && arrayBContainsOnlyNulls)
                        return true;

                    System.Diagnostics.Debug.WriteLine($"ERROR: Data Array Length Mismatch! {property.Name}");
                    System.Diagnostics.Debug.WriteLine($"     {adesc} = '{a}' Length={arrayA.Length} {ta.Namespace}.{ta.Name}");
                    System.Diagnostics.Debug.WriteLine($"     {bdesc} = '{b}' Length={arrayB.Length} {tb.Namespace}.{tb.Name}");
                    return false;
                }

                for(int i =0; i < arrayA.Length; i++)
                {
                    res &= CompareGeneric(arrayA[i], arrayB[i], adesc, bdesc, property, ta, tb);
                }

            }

            return res;
        }


        public static bool Same(object a, string adesc, object b, string bdesc)
        {
            // DASH Objects contain "circular references" to parent object
            // This requires to check what we already have processed or
            // we'll loop forever (or until stack is out)...
            //
            HashSet<Tuple<object, object>> Processed = new HashSet<Tuple<object, object>>();

            bool res = SameInternal(a, adesc, b, bdesc, Processed);

            Processed.Clear();

            Processed = null;

            return res;
        }
        protected static bool SameInternal(object a, string adesc, object b, string bdesc, HashSet<Tuple<object, object>>Processed)
        {
            bool result = true;

            // Both null, both same...
            if (a == null && b == null)
                return true;

            Processed.Add(new Tuple<object, object>(a, b));

            if (a == null || b == null)
            {
                System.Diagnostics.Debug.WriteLine($"Null Object Mismatch!:");
                System.Diagnostics.Debug.WriteLine($"{adesc}={a?.GetType()}");
                System.Diagnostics.Debug.WriteLine($"{bdesc}={b?.GetType()}");

                return false;
            }

            Type ta = a.GetType();
            Type tb = b.GetType();

            PropertyInfo[] pa = ta.GetProperties();
            PropertyInfo[] pb = ta.GetProperties();

            IEnumerable<PropertyInfo> common = pa.Intersect(pb);

            if (common.Count() != pa.Count() || common.Count() != pb.Count())
            {
                System.Diagnostics.Debug.WriteLine($"Property Count Mismatch! {adesc} {ta.Name}={pa.Count()} {bdesc} {tb.Name}={pb.Count()} Common={common.Count()}");
            }

            // Use union of two properties (for name extraction purposes)
            foreach (var property in common)
            {
                object[] adata;
                object[] bdata;

                // Return all data in a form of an array
                // Simplifies handling for arrayed/non arrayed data
                try
                {
                    if (property.PropertyType.IsArray)
                    {
                        adata = ta.GetProperty(property.Name)?.GetValue(a, null) as object[];
                        bdata = tb.GetProperty(property.Name)?.GetValue(b, null) as object[];
                    }
                    else
                    {
                        adata = new object[] { ta.GetProperty(property.Name)?.GetValue(a, null) };
                        bdata = new object[] { tb.GetProperty(property.Name)?.GetValue(b, null) };
                    }
                }catch(Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not retrieve object {property.Name} Array: {property.PropertyType.IsArray}:");
                    System.Diagnostics.Debug.WriteLine($"Exception: {e}");
                    continue;
                }

                // Handle null returns
                // res=null, res[0]=
                // Treat them all same as "no value/empty"
                if (adata?.Length == 0 || bdata?.Length == 0 ||
                    adata?.Length == null || bdata?.Length == null)
                    continue;

                //Make sure item count same...
                if(adata.Length != bdata.Length)
                {
                    System.Diagnostics.Debug.WriteLine($"Extracted property count Mismatch! {property.Name} {adesc}={adata.Length} {bdesc}={bdata.Length}");
                    result &= false;
                    continue;
                }

                Func<object[], object[], string, string, PropertyInfo, Type, Type, bool> ccomp;

                if (CustomComparers.TryGetValue(property.Name, out ccomp))
                {
                    // Custom Field Comparer
                    result &= ccomp(adata, bdata, adesc, bdesc, property, ta, tb);
                }
                else
                {
                    // Default Comparer
                    for (int i = 0; i < adata.Length; i++)
                    {
                        if (adata[i] == null && bdata[i] == null)
                            continue;

                        // Is there a better way to differentiate "system" data types from
                        // application defined data types?
                        // Assumption being - system data types are comparable with each other
                        // "as is", while user defined data types need to be decomposed into
                        // individual system elements
                        if (property.PropertyType.Namespace.StartsWith("System", StringComparison.OrdinalIgnoreCase))
                        {
                            result &= CompareGeneric(adata[i], bdata[i], adesc, bdesc, property, ta, tb);
                        }
                        else
                        {
                            //App defined object, try to decompose ONLY if not done before
                            if (!Processed.Contains(new Tuple<object, object>(adata[i], bdata[i])))
                            {
                                result &= SameInternal(adata[i], adesc, bdata[i], bdesc, Processed);
                            }

                        }
                    }
                }

            }

            return result;
        }

        protected static void DisplayMissingFields(Type input, Type output, object[] data)
        {
            if (Array.TrueForAll<object>(data, ovalue => { return ovalue == null; }))
                return;

            System.Diagnostics.Debug.WriteLine($"Information: Fields exist in input ({input.ToString()}) but not in destination ({output.ToString()})");

            foreach(var o in data)
            {
                if (o == null)
                    continue;

                Type t = o.GetType();

                if (t.IsArray)
                {
                    System.Diagnostics.Debug.WriteLine($"     Name: {t.Namespace}.{t.Name}");
                    object[] array = o as object[];
                    foreach(var subo in array)
                    {
                        var val = subo?.GetType().GetProperty("Value")?.GetValue(subo, null);
                        System.Diagnostics.Debug.WriteLine($"     Data: {val}");
                    }
                }
                else
                {
                    var val = t.GetProperty("Value")?.GetValue(o, null);
                    System.Diagnostics.Debug.WriteLine(o != null, $"     Name: {t?.Name} Data:{val}");
                }
            }
        }
        public static DASH Convert(MPDtype mpd, string url)
        {
            DASHWriter dw = new DASHWriter(url);

            // System Parsed XML do not have Xmlns in them.
            // Well, they do but in class attributes (if there is a mismatch, nothing will parse)
            // so if we are here... assume correct one.
            dw.Xmlns = "urn:mpeg:dash:schema:mpd:2011";

            if (mpd.ProgramInformation != null)
                dw.ProgramInformations = Array.ConvertAll(mpd.ProgramInformation,
                    new Converter<ProgramInformationType, ProgramInformation>(ConvertProgramInformation));

            if (mpd.BaseURL != null)
                dw.BaseURLs = Array.ConvertAll(mpd.BaseURL,
                    new Converter<BaseURLType, BaseURL>(ConvertBaseURLs));

            dw.Locations = mpd.Location;

            if (mpd.Period != null)
            {
                currDoc = dw;
                dw.Periods = Array.ConvertAll(mpd.Period, new Converter<PeriodType, Period>(ConvertPeriods));
                currDoc = null;
            }

            if (mpd.Metrics != null)
                dw.Metrics = Array.ConvertAll(mpd.Metrics,
                    new Converter<MetricsType, Metrics>(ConvertMetrics));

            dw.Id = mpd.id;
            dw.Profiles = mpd.profiles;
            dw.Type = mpd.type.ToString();


            if (mpd.availabilityStartTimeSpecified)
                dw.AvailabilityStartTime = mpd.availabilityStartTime;

            if(mpd.availabilityEndTimeSpecified)
                dw.AvailabilityEndTime = mpd.availabilityEndTime;

            if(mpd.publishTimeSpecified)
                dw.PublishTime = mpd.publishTime;

            if (mpd.mediaPresentationDuration != null)
                dw.MediaPresentationDuration = XmlConvert.ToTimeSpan(mpd.mediaPresentationDuration);

            if (mpd.minimumUpdatePeriod != null)
                dw.MinimumUpdatePeriod = XmlConvert.ToTimeSpan(mpd.minimumUpdatePeriod);

            if (mpd.minBufferTime != null)
                dw.MinBufferTime = XmlConvert.ToTimeSpan(mpd.minBufferTime);

            if (mpd.timeShiftBufferDepth != null)
                dw.TimeShiftBufferDepth = XmlConvert.ToTimeSpan(mpd.timeShiftBufferDepth);

            if (mpd.suggestedPresentationDelay != null)
                dw.SuggestedPresentationDelay = XmlConvert.ToTimeSpan(mpd.suggestedPresentationDelay);

            if (mpd.maxSegmentDuration != null)
                dw.MaxSegmentDuration = XmlConvert.ToTimeSpan(mpd.maxSegmentDuration);

            if (mpd.maxSubsegmentDuration != null)
                dw.MaxSubsegmentDuration = XmlConvert.ToTimeSpan(mpd.maxSubsegmentDuration);


            // Missing
            //
            // mpd.EssentialProperty;
            // mpd.SupplementalProperty;
            // mpd.UTCTiming;

            DisplayMissingFields(mpd.GetType(), dw.GetType(), new object[]
                {mpd.EssentialProperty, mpd.SupplementalProperty, mpd.UTCTiming});
            return (dw as DASH);

        }

        protected static ProgramInformation ConvertProgramInformation(ProgramInformationType input)
        {
            ProgramInformationWriter item = new ProgramInformationWriter();

            if(input.Title != null)
                item.Titles = new string[] { input.Title };

            if (input.Source != null)
                item.Sources = new string[] { input.Source };

            if (input.Copyright != null)
                item.Copyrights = new string[] { input.Copyright };

            item.Lang = input.lang;
            item.MoreInformationURL = input.moreInformationURL;

            return item;
        }
        protected static Metrics ConvertMetrics(MetricsType input)
        {
            MetricsWriter item = new MetricsWriter();

            if (input.Reporting != null)
                item.Reportings = Array.ConvertAll(input.Reporting,
                    new Converter<DescriptorType, Descriptor>(ConvertDescriptors));

            if (input.Range != null)
                item.Ranges = Array.ConvertAll(input.Range,
                    new Converter<RangeType, MpdParser.Node.Range>(ConvertRanges));

            item.MetricsAttr = input.metrics;

            return item;
        }

        protected static MpdParser.Node.Range ConvertRanges(RangeType input)
        {
            RangeWriter item = new RangeWriter();

            if(input.starttime != null)
                item.StartTime = XmlConvert.ToTimeSpan(input.starttime);

            if(input.duration != null)
                item.Duration = XmlConvert.ToTimeSpan(input.duration);

            return item;
        }
        protected static BaseURL ConvertBaseURLs(BaseURLType input)
        {
            BaseURLWriter item = new BaseURLWriter();

            item.BaseUrlValue = input.Value;
            item.ServiceLocation = input.serviceLocation;
            item.ByteRange = input.byteRange;

            if (input.availabilityTimeOffsetSpecified)
                item.AvailabilityTimeOffset = input.availabilityTimeOffset;

            if (input.availabilityTimeCompleteSpecified)
                item.AvailabilityTimeComplete = item.AvailabilityTimeComplete;

            return item;
        }

        protected static Period ConvertPeriods(PeriodType input)
        {
            PeriodWriter item = new PeriodWriter(currDoc);

            if (input.BaseURL != null)
                item.BaseURLs = Array.ConvertAll(input.BaseURL,
                    new Converter<BaseURLType, BaseURL>(ConvertBaseURLs));

            if (input.SegmentBase != null)
                item.SegmentBases = Array.ConvertAll(new SegmentBaseType[] { input.SegmentBase },
                    new Converter<SegmentBaseType, SegmentBase>(ConvertSegmentBases));

            if (input.SegmentList != null)
                item.SegmentLists = Array.ConvertAll(new SegmentListType[] { input.SegmentList },
                    new Converter<SegmentListType, SegmentList>(ConvertSegmentLists));
            if (input.SegmentTemplate != null)
                item.SegmentTemplates = Array.ConvertAll(new SegmentTemplateType[] { input.SegmentTemplate },
                    new Converter<SegmentTemplateType, SegmentTemplate>(ConvertSegmentTemplates));

            if (input.AssetIdentifier != null)
                item.AssetIdentifiers = Array.ConvertAll(new DescriptorType[] { input.AssetIdentifier },
                    new Converter<DescriptorType, Descriptor>(ConvertDescriptors));

            if(input.EventStream != null)
                item.EventStreams = Array.ConvertAll(input.EventStream,
                    new Converter<EventStreamType, EventStream>(ConvertEventStreams));

            if (input.AdaptationSet != null)
            {
                currPeriod = item as Period;

                item.AdaptationSets = Array.ConvertAll(input.AdaptationSet,
                    new Converter<AdaptationSetType, AdaptationSet>(ConvertAdaptationSets));

                currPeriod = null;
            }

            if (input.Subset != null)
                item.Subsets = Array.ConvertAll(input.Subset,
                    new Converter<SubsetType, Subset>(ConvertSubsets));

            item.Id = input.id;

            if (input.start != null)
                item.Start = XmlConvert.ToTimeSpan(input.start);

            if(input.duration != null)
                item.Duration = XmlConvert.ToTimeSpan(input.duration);

            item.BitstreamSwitching = input.bitstreamSwitching;

            //Missing
            //
            //input.SupplementalProperty
            //input.href
            //input.actuate

            DisplayMissingFields(input.GetType(), item.GetType(), new object[]
                { input.SupplementalProperty, input.actuate });
            return item;
        }

        protected static SegmentBase ConvertSegmentBases(SegmentBaseType input)
        {
            SegmentBaseWriter item = new SegmentBaseWriter();

            if (input.Initialization != null)
                item.Initializations = Array.ConvertAll(new URLType[] { input.Initialization },
                    new Converter<URLType, URL>(ConvertURLs));

            if (input.RepresentationIndex != null)
                item.RepresentationIndexes = Array.ConvertAll(new URLType[] { input.RepresentationIndex },
                new Converter<URLType, URL>(ConvertURLs));

            if (input.timescaleSpecified)
                item.Timescale = input.timescale;

            if (input.presentationTimeOffsetSpecified)
                item.PresentationTimeOffset = input.presentationTimeOffset;

            item.IndexRange = input.indexRange;
            item.IndexRangeExact = input.indexRangeExact;

            if(input.availabilityTimeOffsetSpecified)
                item.AvailabilityTimeOffset = input.availabilityTimeOffset;

            if (input.availabilityTimeOffsetSpecified)
                item.AvailabilityTimeComplete = input.availabilityTimeComplete;

           return item;


        }

        protected static URL ConvertURLs(URLType input)
        {
            URLWriter item = new URLWriter();

            item.SourceURL = input.sourceURL;
            item.Range = input.range;

            return item;
        }

        protected static SegmentList ConvertSegmentLists(SegmentListType input)
        {
            SegmentListWriter item = new SegmentListWriter();

            if (input.SegmentURL != null)
                item.SegmentURLs = Array.ConvertAll(input.SegmentURL,
                    new Converter<SegmentURLType, SegmentURL>(ConvertSegmentURLs));

            var subitem = item as MultipleSegmentBase;
            var tmp = ConvertMultipleSegmentBase(input);
            subitem.FillProperties(tmp);

            return item;
        }

        protected static SegmentTemplate ConvertSegmentTemplates(SegmentTemplateType input)
        {
            SegmentTemplateWriter item = new SegmentTemplateWriter();

            if (input.media != null)
                item.Media = new MpdParser.Node.Template(input.media);

            if (input.index != null)
                item.Index = new MpdParser.Node.Template(input.index);

            if (input.initialization != null)
                item.Initialization = new MpdParser.Node.Template(input.initialization);

            item.BitstreamSwitching = input.bitstreamSwitching;

            var subitem = item as MultipleSegmentBase;
            var tmp = ConvertMultipleSegmentBase(input);
            subitem.FillProperties(tmp);

            return item;
        }

        protected static Descriptor ConvertDescriptors(DescriptorType input)
        {
            DescriptorWriter item = new DescriptorWriter();

            item.SchemeIdUri = input.schemeIdUri;
            item.Value = input.value;
            item.Id = input.id;

            return item;
        }

        protected static EventStream ConvertEventStreams(EventStreamType input)
        {
            EventStreamWriter item = new EventStreamWriter();

            item.SchemeIdUri = input.schemeIdUri;
            item.Value = input.value;
            item.Timescale = input.timescale;

            if (input.Event != null)
                item.Events = Array.ConvertAll(input.Event, new Converter<EventType, Event>(ConvertEvents));

            return item;
        }

        protected static Event ConvertEvents(EventType input)
        {
            EventWriter item = new EventWriter();

            // Odd. Presentation time filed in our case seems "optional" while
            // xsd generated data structures treat this as mandatory...
            item.PresentationTime = input.presentationTime;

            if (input.durationSpecified)
                item.Duration = input.duration;

            if (input.idSpecified)
                item.Id = input.id;


            item.EventValue = input.messageData;

            return item;
        }
        protected static AdaptationSet ConvertAdaptationSets(AdaptationSetType input)
        {
            AdaptationSetWriter item = new AdaptationSetWriter(currPeriod);

            if (input.Accessibility != null)
                item.Accessibilities = Array.ConvertAll(input.Accessibility,
                    new Converter<DescriptorType, Descriptor>(ConvertDescriptors));

            if (input.Role!= null)
                item.Roles = Array.ConvertAll(input.Role,
                    new Converter<DescriptorType, Descriptor>(ConvertDescriptors));

            if (input.Rating != null)
                item.Ratings = Array.ConvertAll(input.Rating,
                    new Converter<DescriptorType, Descriptor>(ConvertDescriptors));

            if (input.Viewpoint != null)
                item.Viewpoints = Array.ConvertAll(input.Viewpoint,
                    new Converter<DescriptorType, Descriptor>(ConvertDescriptors));

            if (input.ContentComponent != null)
                item.ContentComponents = Array.ConvertAll(input.ContentComponent,
                    new Converter<ContentComponentType, ContentComponent>(ConvertContentComponents));

            if (input.BaseURL != null)
                item.BaseURLs = Array.ConvertAll(input.BaseURL,
                    new Converter<BaseURLType, BaseURL>(ConvertBaseURLs));

            if (input.SegmentBase != null)
                item.SegmentBases = Array.ConvertAll(new SegmentBaseType[] { input.SegmentBase },
                    new Converter<SegmentBaseType, SegmentBase>(ConvertSegmentBases));

            if (input.SegmentList != null)
                item.SegmentLists = Array.ConvertAll(new SegmentListType[] { input.SegmentList },
                    new Converter<SegmentListType, SegmentList>(ConvertSegmentLists));

            if (input.SegmentTemplate != null)
                item.SegmentTemplates = Array.ConvertAll(new SegmentTemplateType[] { input.SegmentTemplate },
                    new Converter<SegmentTemplateType, SegmentTemplate>(ConvertSegmentTemplates));

            if (input.Representation != null)
            {
                currAdaptationSet = item;

                item.Representations = Array.ConvertAll(input.Representation,
                    new Converter<RepresentationType, Representation>(ConvertRepresentations));

                currAdaptationSet = null;
            }



            if (input.idSpecified)
                item.Id = input.id;

            if (input.groupSpecified)
                item.Group = input.group;

            item.Lang = input.lang;
            item.ContentType = input.contentType;
            item.Par = input.par;

            if (input.minBandwidthSpecified)
                item.MinBandwidth = input.minBandwidth;

            if (input.maxBandwidthSpecified)
                item.MaxBandwidth = input.maxBandwidth;

            if (input.minBandwidthSpecified)
                item.MinBandwidth = input.minBandwidth;

            if (input.minWidthSpecified)
                item.MinWidth = input.minWidth;

            if (input.maxWidthSpecified)
                item.MaxWidth = input.maxWidth;

            if (input.minHeightSpecified)
                item.MinHeight = input.minHeight;

            if (input.maxHeightSpecified)
                item.MaxHeight = input.maxHeight;

            item.MinFrameRate = input.minFrameRate;
            item.MaxFrameRate = input.maxFrameRate;
            item.SegmentAlignment = input.segmentAlignment;
            item.SubsegmentAlignment = input.subsegmentAlignment;

            // Again = subsegmentStartsWithSAP is always present...
            // and int/uint data type conversion...
            item.SubsegmentStartsWithSAP = (int)input.subsegmentStartsWithSAP;

            if (input.bitstreamSwitchingSpecified)
                item.BitstreamSwitching = input.bitstreamSwitching;

            // All the bases are belong to us...
            var subItem = item as RepresentationBase;
            var tmp = ConvertRepresentationBases(input);
            subItem.FillProperties(tmp);

            // Missing
            // input.href
            // input.actuate
            DisplayMissingFields(input.GetType(), item.GetType(), new object[]
                { input.href, input.actuate });

            return item;
        }

        protected static RepresentationBase ConvertRepresentationBases(RepresentationBaseType input)
        {
            RepresentationBaseWriter item = new RepresentationBaseWriter();

            if (input.FramePacking != null)
                item.FramePackings = Array.ConvertAll(input.FramePacking,
                    new Converter<DescriptorType, Descriptor>(ConvertDescriptors));

            if (input.AudioChannelConfiguration != null)
                item.AudioChannelConfigurations = Array.ConvertAll(input.AudioChannelConfiguration,
                    new Converter<DescriptorType, Descriptor>(ConvertDescriptors));

            if (input.ContentProtection != null)
                item.ContentProtections = Array.ConvertAll(input.ContentProtection,
                    new Converter<DescriptorType, ContentProtection>(ConvertContentProtections));

            if(input.EssentialProperty != null)
                item.EssentialProperties = Array.ConvertAll(input.EssentialProperty,
                    new Converter<DescriptorType, Descriptor>(ConvertDescriptors));

            if (input.SupplementalProperty != null)
                item.SupplementalProperties = Array.ConvertAll(input.SupplementalProperty,
                    new Converter<DescriptorType, Descriptor>(ConvertDescriptors));

            if (input.InbandEventStream != null)
                item.InbandEventStreams = Array.ConvertAll(input.InbandEventStream,
                    new Converter<EventStreamType, Descriptor>(ConvertInbandEvent));

            item.Profiles = input.profiles;

            if (input.widthSpecified)
                item.Width = input.width;

            if (input.heightSpecified)
                item.Height = input.height;

            item.Sar = input.sar;
            item.FrameRate = input.frameRate;
            item.AudioSamplingRate = input.audioSamplingRate;
            item.MimeType = input.mimeType;
            item.SegmentProfiles = input.segmentProfiles;
            item.Codecs = input.codecs;

            if (input.maximumSAPPeriodSpecified)
                item.MaximumSAPPeriod = input.maximumSAPPeriod;

            if (input.startWithSAPSpecified)
                item.StartWithSAP = input.startWithSAP;

            if (input.maxPlayoutRateSpecified)
                item.MaxPlayoutRate = input.maxPlayoutRate;

            if (input.codingDependencySpecified)
                item.CodingDependency = input.codingDependency;

            if (input.scanTypeSpecified)
                item.ScanType = input.scanType.ToString();

            return item;
        }

        protected static Descriptor ConvertInbandEvent(EventStreamType input)
        {
            DescriptorWriter item = new DescriptorWriter();

            item.SchemeIdUri = input.schemeIdUri;
            item.Value = input.value;

            // Missing
            //
            // input.Event;
            // input.href;
            // input.actuate

            // Unmapped
            // MpdParser.Node.Descriptor.Id

            DisplayMissingFields(input.GetType(), item.GetType(), new object[]
                { input.Event, input.href, input.actuate });

            DisplayMissingFields(item.GetType(), item.GetType(), new object[]
                { item.Id });

            return item;
        }
        protected static ContentProtection ConvertContentProtections(DescriptorType input)
        {
            ContentProtectionWriter item = new ContentProtectionWriter();


            if (input.schemeIdUri != null)
                item.SchemeIdUri = input.schemeIdUri;

            if (input.value != null)
                item.Value = input.value;

            if (input.Any != null)
            {
                item.Data = "<xml>";
                foreach(var d in input.Any)
                {
                    item.Data += d.OuterXml.Trim(new char[] {'\r','\n','\t',' ' });
                }
                item.Data += "</xml>";
            }

            if(input.AnyAttr != null)
            {
                foreach(var attrib in input.AnyAttr)
                {
                    if(attrib.Name == "cenc:default_KID")
                    {
                        item.CencDefaultKID = attrib.InnerText;
                    }
                }
            }
             return item;

        }
        protected static Representation ConvertRepresentations(RepresentationType input)
        {
            RepresentationWriter item = new RepresentationWriter(currAdaptationSet);

            if (input.BaseURL != null)
                item.BaseURLs = Array.ConvertAll(input.BaseURL,
                    new Converter<BaseURLType, BaseURL>(ConvertBaseURLs));

            if (input.SubRepresentation != null)
                item.SubRepresentations = Array.ConvertAll(input.SubRepresentation,
                    new Converter<SubRepresentationType, SubRepresentation>(ConvertSubRepresentations));

            if (input.SegmentBase != null)
                item.SegmentBases = Array.ConvertAll(new SegmentBaseType[] { input.SegmentBase },
                    new Converter<SegmentBaseType, SegmentBase>(ConvertSegmentBases));

            if (input.SegmentList != null)
                item.SegmentLists = Array.ConvertAll(new SegmentListType[] { input.SegmentList },
                    new Converter<SegmentListType, SegmentList>(ConvertSegmentLists));

            if (input.SegmentTemplate != null)
                item.SegmentTemplates = Array.ConvertAll(new SegmentTemplateType[] { input.SegmentTemplate },
                    new Converter<SegmentTemplateType, SegmentTemplate>(ConvertSegmentTemplates));

            item.Id = input.id;
            item.Bandwidth = input.bandwidth;

            if (input.qualityRankingSpecified)
                item.QualityRanking = input.qualityRanking;

            if (input.dependencyId != null)
                item.DependencyId = input.dependencyId;

            var subitem = item as RepresentationBase;
            var tmp = ConvertRepresentationBases(input);
            subitem.FillProperties(tmp);

            // Missing
            // input.mediaStreamStructure
            //
            // Does not exit in input
            //MprdParser.Node.Representation.NumChannels
            //MpdParser.Node.Representation.SampleRate

            DisplayMissingFields(input.GetType(), item.GetType(), new object[]
                {input.mediaStreamStructureId });

            return item;


        }

        protected static SubRepresentation ConvertSubRepresentations(SubRepresentationType input)
        {
            SubRepresentationWriter item = new SubRepresentationWriter();

            if (input.levelSpecified)
                item.Level = input.level;

            item.DependencyLevel = input.dependencyLevel;

            if (input.bandwidthSpecified)
                item.Bandwidth = input.bandwidth;


            item.ContentComponent = input.contentComponent;

            var subitem = item as RepresentationBase;
            var tmp = ConvertRepresentationBases(input);
            subitem.FillProperties(tmp);

            return item;
        }

        protected static ContentComponent ConvertContentComponents(ContentComponentType input)
        {
            ContentComponentWriter item = new ContentComponentWriter();

            if (input.Accessibility != null)
                item.Accessibilities = Array.ConvertAll(input.Accessibility,
                    new Converter<DescriptorType, Descriptor>(ConvertDescriptors));

            if(input.Role != null)
                item.Roles = Array.ConvertAll(input.Role,
                    new Converter<DescriptorType, Descriptor>(ConvertDescriptors));

            if (input.Rating != null)
                item.Ratings = Array.ConvertAll(input.Rating,
                    new Converter<DescriptorType, Descriptor>(ConvertDescriptors));

            if (input.Viewpoint != null)
                item.Viewpoints = Array.ConvertAll(input.Viewpoint,
                    new Converter<DescriptorType, Descriptor>(ConvertDescriptors));



            if (input.idSpecified)
                item.Id = input.id;

            item.Lang = input.lang;
            item.ContentType = input.contentType;
            item.Par = input.par;

            return item;
        }
        protected static Subset ConvertSubsets(SubsetType input)
        {
            SubsetWriter item = new SubsetWriter();

            // int/uint type mismatch?
            if (input.contains != null)
                item.Contains = (int[])(object)input.contains;

            item.Id = input.id;

            return item;
        }

        protected static SegmentURL ConvertSegmentURLs(SegmentURLType input)
        {
            SegmentURLWriter item = new SegmentURLWriter();

            item.Media = input.media;
            item.MediaRange = input.mediaRange;
            item.Index = input.index;
            item.IndexRange = input.indexRange;

            return item;
        }

        protected static MultipleSegmentBase ConvertMultipleSegmentBase(MultipleSegmentBaseType input)
        {
            MultipleSegmentBaseWriter item = new MultipleSegmentBaseWriter();

            if (input.SegmentTimeline != null)
                item.SegmentTimelines = Array.ConvertAll(new SegmentTimelineType[] { input.SegmentTimeline },
                    new Converter<SegmentTimelineType, SegmentTimeline>(ConvertSegmentTimeline));

            if (input.BitstreamSwitching != null)
                item.BitstreamSwitchings = Array.ConvertAll(new URLType[] { input.BitstreamSwitching },
                    new Converter<URLType, URL>(ConvertURLs));

            if (input.durationSpecified)
                item.Duration = input.duration;

            if (input.startNumberSpecified)
                item.StartNumber = input.startNumber;

            var subItem = ((SegmentBase) item);
            var tmp = ConvertSegmentBases(input);
            subItem.FillProperties(tmp);


            return item;
        }

        protected static SegmentTimeline ConvertSegmentTimeline(SegmentTimelineType input)
        {
            SegmentTimelineWriter item = new SegmentTimelineWriter();

            if (input.S != null)
                item.Ss = Array.ConvertAll(input.S, new Converter<SegmentTimelineTypeS, S>(SConverter));

            return item;
        }

        protected static S SConverter(SegmentTimelineTypeS input)
        {
            SWriter item = new SWriter();

            if (input.tSpecified)
                item.T = input.t;

            item.D = input.d;

            if (input.r != null)
                item.R = Int32.Parse(input.r);

            //Missing
            //

            // input.nSpecified
            // input.n
            DisplayMissingFields(input.GetType(), item.GetType(), new object[]
                {input.nSpecified, input.n });
            return item;
        }


    }

}