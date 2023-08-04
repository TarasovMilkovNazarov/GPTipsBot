using Newtonsoft.Json;

namespace GPTipsBot.InsightFaceSwap.FaceSwap
{
    public class Attachment
    {
        public string filename;
        public string id { get; set; }
        public string upload_filename { get; set; }
        public string uploaded_filename { get; set; }
        public string upload_url { get; set; }

        [JsonIgnore]
        public byte[] file { get; set; }
    }
}