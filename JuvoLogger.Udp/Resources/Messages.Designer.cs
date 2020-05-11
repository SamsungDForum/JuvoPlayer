﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace JuvoLogger.Udp.Resources {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Messages {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Messages() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("JuvoLogger.Udp.Resources.Messages", typeof(Messages).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to 
        ///****************************
        ///*   JuvoPlayer UDP logger  *
        ///*                          *  
        ///* 1 UDP packet to:         *
        ///*   - stop output          *
        ///*     logs will be dropped *
        ///*   - start output         *
        ///*   - hijack connection    *
        ///****************************
        ///*         Started          *
        ///****************************
        ///.
        /// </summary>
        internal static string ConnectMessage {
            get {
                return ResourceManager.GetString("ConnectMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to 
        ///****************************
        ///*   Connection hijacked!   *
        ///****************************
        ///.
        /// </summary>
        internal static string HijackMessage {
            get {
                return ResourceManager.GetString("HijackMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to 
        ///****************************
        ///*         Started          *
        ///****************************
        ///.
        /// </summary>
        internal static string StartMessage {
            get {
                return ResourceManager.GetString("StartMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to 
        ///****************************
        ///*         Stopped          *
        ///****************************
        ///.
        /// </summary>
        internal static string StopMessage {
            get {
                return ResourceManager.GetString("StopMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ****************************
        ///*   JuvoPlayer UDP Logger  *
        ///*    LOGGING TERMINATED    *
        ///****************************.
        /// </summary>
        internal static string TerminationMessage {
            get {
                return ResourceManager.GetString("TerminationMessage", resourceCulture);
            }
        }
    }
}
