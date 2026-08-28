using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Fbx2Vmd.Settings
{
    public sealed class MainRecordingSettingsStore
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public MainRecordingSettingsStore(string settingsFilePath = null)
        {
            SettingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
                ? MainRecordingSettingsPathResolver.ResolveSettingsFilePath()
                : settingsFilePath.Trim();
        }

        public string SettingsFilePath { get; }

        public MainRecordingSettingsDocument LoadOrCreateDefault()
        {
            if (!File.Exists(SettingsFilePath))
            {
                return MainRecordingSettingsDocumentNormalizer.Normalize(null);
            }

            string json = File.ReadAllText(SettingsFilePath, Utf8NoBom);
            if (string.IsNullOrWhiteSpace(json))
            {
                return MainRecordingSettingsDocumentNormalizer.Normalize(null);
            }

            try
            {
                MainRecordingSettingsDocument document = JsonUtility.FromJson<MainRecordingSettingsDocument>(json);
                return MainRecordingSettingsDocumentNormalizer.Normalize(document);
            }
            catch (ArgumentException)
            {
                BackupCorruptFile();
                return MainRecordingSettingsDocumentNormalizer.Normalize(null);
            }
        }

        internal DateTime ResolveLastWriteTimeUtc()
        {
            return File.Exists(SettingsFilePath)
                ? File.GetLastWriteTimeUtc(SettingsFilePath)
                : DateTime.MinValue;
        }

        public void Save(MainRecordingSettingsDocument document)
        {
            MainRecordingSettingsDocument normalized =
                MainRecordingSettingsDocumentNormalizer.Normalize(document);
            normalized.updatedAtUtc = DateTime.UtcNow.ToString("O");

            string directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempPath = CreateTempPath();
            File.WriteAllText(tempPath, JsonUtility.ToJson(normalized, true), Utf8NoBom);

            if (File.Exists(SettingsFilePath))
            {
                File.Replace(tempPath, SettingsFilePath, null);
                return;
            }

            File.Move(tempPath, SettingsFilePath);
        }

        private void BackupCorruptFile()
        {
            if (!File.Exists(SettingsFilePath))
            {
                return;
            }

            string backupPath = SettingsFilePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            File.Move(SettingsFilePath, backupPath);
        }

        private string CreateTempPath()
        {
            return SettingsFilePath + ".tmp-" + Guid.NewGuid().ToString("N");
        }
    }
}
