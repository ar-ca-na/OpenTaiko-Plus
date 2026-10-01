using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace TJAPlayer3
{
    /// <summary>
    /// 設定ファイル入出力クラス。
    /// </summary>
    public static class ConfigManager
    {
        /// <summary>
        /// 設定ファイルは必ず OpenTaiko.exe の隣に置く。
        /// もとは "NamePlate.json" のような相対パスをそのまま使っていたので、
        /// 作業フォルダ次第で別の場所を読み書きしていた。
        /// .tja を .bat にドロップして書き出すと作業フォルダが曲のフォルダになり、
        /// そこに既定値の NamePlate.json が作られて、
        /// プレイヤー名や称号が設定どおりに出なかった。
        /// </summary>
        private static string t本体の隣にする(string filePath)
        {
            try
            {
                if (System.IO.Path.IsPathRooted(filePath)) return filePath;
                string exeDir = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(exeDir)) return filePath;
                return System.IO.Path.Combine(exeDir, filePath);
            }
            catch
            {
                return filePath;
            }
        }

        private static readonly JsonSerializerSettings Settings =
            new JsonSerializerSettings()
            {
                ObjectCreationHandling = ObjectCreationHandling.Auto,
                DefaultValueHandling = DefaultValueHandling.Include,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = new StringEnumConverter[] { new StringEnumConverter() }
            };

        /// <summary>
        /// 設定ファイルの読み込みを行います。ファイルが存在しなかった場合、そのクラスの新規インスタンスを返します。
        /// </summary>
        /// <typeparam name="T">シリアライズしたクラス。</typeparam>
        /// <param name="filePath">ファイル名。</param>
        /// <returns>デシリアライズ結果。</returns>
        public static T GetConfig<T>(string filePath) where T : new()
        {
            filePath = t本体の隣にする(filePath);
            var json = "";
            if (!System.IO.File.Exists(filePath))
            {
                // ファイルが存在しないので
                SaveConfig(new T(), filePath);
            }
            using (var stream = new System.IO.StreamReader(filePath, Encoding.UTF8))
            {
                json = stream.ReadToEnd();
            }
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }

        /// <summary>
        /// 設定ファイルの書き込みを行います。
        /// </summary>
        /// <param name="obj">シリアライズするインスタンス。</param>
        /// <param name="filePath">ファイル名。</param>
        public static void SaveConfig(object obj, string filePath)
        {
            filePath = t本体の隣にする(filePath);
            using (var stream = new System.IO.StreamWriter(filePath, false, Encoding.UTF8))
            {
                stream.Write(JsonConvert.SerializeObject(obj, Formatting.Indented, Settings));
            }
        }
    }
}
