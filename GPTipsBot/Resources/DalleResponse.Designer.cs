﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace GPTipsBot.Resources {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class DalleResponse {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal DalleResponse() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("GPTipsBot.Resources.DalleResponse", typeof(DalleResponse).Assembly);
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
        ///   Looks up a localized string similar to Не удалось сгенерировать изображение. Попробуйте изменить запрос..
        /// </summary>
        internal static string BadImagesError {
            get {
                return ResourceManager.GetString("BadImagesError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ⚠️Ваш запрос был заблокирован. Попробуйте изменить некорректные слова и повторите попытку..
        /// </summary>
        internal static string BlockedPromptError {
            get {
                return ResourceManager.GetString("BlockedPromptError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Выберите игру.
        /// </summary>
        internal static string ChooseGame {
            get {
                return ResourceManager.GetString("ChooseGame", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Изображения не найдены..
        /// </summary>
        internal static string NoImagesError {
            get {
                return ResourceManager.GetString("NoImagesError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Не удалось получить результаты..
        /// </summary>
        internal static string NoResultsError {
            get {
                return ResourceManager.GetString("NoResultsError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Превышен лимит запросов.
        /// </summary>
        internal static string RateLimit {
            get {
                return ResourceManager.GetString("RateLimit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ⚠️Не удалось выполнить перенаправление..
        /// </summary>
        internal static string RedirectError {
            get {
                return ResourceManager.GetString("RedirectError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ⚠️Ваш запрос превысил время ожидания..
        /// </summary>
        internal static string TimeoutError {
            get {
                return ResourceManager.GetString("TimeoutError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Данный язык в настоящее время не поддерживается..
        /// </summary>
        internal static string UnsupportedLangError {
            get {
                return ResourceManager.GetString("UnsupportedLangError", resourceCulture);
            }
        }
    }
}
