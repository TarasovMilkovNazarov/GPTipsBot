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
    public class BotResponse {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal BotResponse() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("GPTipsBot.Resources.BotResponse", typeof(BotResponse).Assembly);
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
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to 🤖 Добро пожаловать в бот ChatGPT! 🚀
        ///
        ///Задавайте вопросы ChatGPT - искусственному интеллекту на основе архитектуры GPT-3.5, разработанному OpenAI. 💬
        ///
        ///- чтобы задать вопрос боту, просто отправьте текст
        ///- для генерации изображения введите команду /image, либо воспользуйтесь кнопкой меню.
        /// </summary>
        public static string BotDescription {
            get {
                return ResourceManager.GetString("BotDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ChatGPT + DALL-E | Бесплатный.
        /// </summary>
        public static string BotName {
            get {
                return ResourceManager.GetString("BotName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Операция отменена.
        /// </summary>
        public static string Cancel {
            get {
                return ResourceManager.GetString("Cancel", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Пожалуйста выберите игру.
        /// </summary>
        public static string ChooseGame {
            get {
                return ResourceManager.GetString("ChooseGame", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Пожалуйста выберите язык интерфейса.
        /// </summary>
        public static string ChooseLanguagePlease {
            get {
                return ResourceManager.GetString("ChooseLanguagePlease", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Контекст успешно сброшен. Начните новый диалог.
        /// </summary>
        public static string ContextUpdated {
            get {
                return ResourceManager.GetString("ContextUpdated", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Куки для Bing image обновлены.
        /// </summary>
        public static string CookiesUpdated {
            get {
                return ResourceManager.GetString("CookiesUpdated", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to GPTipDev.
        /// </summary>
        public static string DevBotName {
            get {
                return ResourceManager.GetString("DevBotName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Привет!:) Чем могу помочь?.
        /// </summary>
        public static string Greeting {
            get {
                return ResourceManager.GetString("Greeting", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Текстовое описание изображения должно быть не более {0} символов. Сократите описание и начните заново с команды /image.
        /// </summary>
        public static string ImageDescriptionLimitWarning {
            get {
                return ResourceManager.GetString("ImageDescriptionLimitWarning", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Чтобы продолжить введите текстовое описание длиной до {0} символов. Для выхода из режима генерации изображений введите /cancel или нажмите кнопку &apos;Отмена&apos;.
        /// </summary>
        public static string InputImageDescriptionText {
            get {
                return ResourceManager.GetString("InputImageDescriptionText", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Руссий язык используется для интерфейса.
        /// </summary>
        public static string LanguageWasSetSuccessfully {
            get {
                return ResourceManager.GetString("LanguageWasSetSuccessfully", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Извините, но сейчас доступна обработка только текстовых сообщений. В разработке находятся голосовые сообщения и генерация картинок текстом.
        /// </summary>
        public static string OnlyMessagesAvailable {
            get {
                return ResourceManager.GetString("OnlyMessagesAvailable", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ⚙️ Ведутся технические работы. Благодарим за понимание..
        /// </summary>
        public static string OnMaintenance {
            get {
                return ResourceManager.GetString("OnMaintenance", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ⌛️ Подождите, подготавливаю ответ....
        /// </summary>
        public static string PleaseWaitMsg {
            get {
                return ResourceManager.GetString("PleaseWaitMsg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Простите, я отутствовал какое-то время, но сейчас снова в строю..
        /// </summary>
        public static string Recovered {
            get {
                return ResourceManager.GetString("Recovered", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Введите сообщение ниже. Будем рады любой обратной связи..
        /// </summary>
        public static string SendFeedback {
            get {
                return ResourceManager.GetString("SendFeedback", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Бот с интеграцией GPT-3.5 + DALL-E. Поможет ответить на вопросы и создать изображение по тексту 🤖👋 (Автор @alanextar).
        /// </summary>
        public static string ShortDescription {
            get {
                return ResourceManager.GetString("ShortDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Служебный бот для разработки, добавляйте @GPTipsBot.
        /// </summary>
        public static string ShortDevDescription {
            get {
                return ResourceManager.GetString("ShortDevDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Что-то пошло не так, попробуйте ещё раз.
        /// </summary>
        public static string SomethingWentWrong {
            get {
                return ResourceManager.GetString("SomethingWentWrong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Сервис генерации изображений находится в тестовом режиме. Попробуйте заново, начиная с команды /image, либо дождитесь исправления ошибок. Мы работаем над улучшением сервиса, благодарим за понимание 🙏.
        /// </summary>
        public static string SomethingWentWrongWithImageService {
            get {
                return ResourceManager.GetString("SomethingWentWrongWithImageService", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Использовать бесплатное прокси: {0}.
        /// </summary>
        public static string SwitchProxy {
            get {
                return ResourceManager.GetString("SwitchProxy", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test.
        /// </summary>
        public static string Test {
            get {
                return ResourceManager.GetString("Test", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Ваш отзыв отправлен автору. Спасибо, что делаете бота лучше!.
        /// </summary>
        public static string Thanks {
            get {
                return ResourceManager.GetString("Thanks", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Количество токенов в вашем сообщении {1} превышает лимит в {0} токенов. Сократите своё сообщение. Считайте, что в одном слове содержится примерно 1 токен.
        /// </summary>
        public static string TokensLimitExceeded {
            get {
                return ResourceManager.GetString("TokensLimitExceeded", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ❗️Слишком много запросов, попробуйте через {0} секунд.
        /// </summary>
        public static string TooManyRequests {
            get {
                return ResourceManager.GetString("TooManyRequests", resourceCulture);
            }
        }
    }
}
