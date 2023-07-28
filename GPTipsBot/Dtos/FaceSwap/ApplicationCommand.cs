namespace GPTipsBot.Dtos.FaceSwap
{
    public class ApplicationCommand
    {
        public static string id { get; set; }
        public static string version { get; set; }
        public ulong application_id { get; set; }
        public object default_member_permissions { get; set; }
        public int type { get; set; }
        public bool nsfw { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public bool dm_permission { get; set; }
        public object contexts { get; set; }
        public List<OptionDescription> options { get; set; }
    }

    public class SwapApplicationCommand : ApplicationCommand
    {
        public SwapApplicationCommand()
        {
            application_id = DiscordSettings.InsightSwapFaceApplicationId;
            type = 1;
            nsfw = false;
            name = "swapid";
            description = "Apply Identity Feature to Target Image, use comma splitter for multiple identities";
            dm_permission = true;
            options = new List<OptionDescription>()
            {
                new OptionDescription
                {
                    type = 3,
                    name = "idname",
                    description = "idname(s) to apply",
                    required = true
                },
                new OptionDescription
                {
                    type = 11,
                    name = "image",
                    description = "target image",
                    required = true
                }
            };
        }
    }

    public class SaveIdApplicationCommand : ApplicationCommand
    {
        public SaveIdApplicationCommand()
        {
            application_id = DiscordSettings.InsightSwapFaceApplicationId;
            type = 1;
            nsfw = false;
            name = "saveid";
            description = "Save Identity Feature by Name and Image";
            dm_permission = true;
            options = new List<OptionDescription>()
            {
                new OptionDescription
                {
                    type = 3,
                    name = "idname",
                    description = "idname to save",
                    required = true
                },
                new OptionDescription
                {
                    type = 11,
                    name = "image",
                    description = "id image",
                    required = true
                }
            };
        }
    }
}