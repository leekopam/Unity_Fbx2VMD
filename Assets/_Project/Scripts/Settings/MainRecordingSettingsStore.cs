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
                return CreateDefaultDocument();
            }

            string json = File.ReadAllText(SettingsFilePath, Utf8NoBom);
            if (string.IsNullOrWhiteSpace(json))
            {
                return CreateDefaultDocument();
            }

            try
            {
                MainRecordingSettingsDocument document = JsonUtility.FromJson<MainRecordingSettingsDocument>(json);
                return NormalizeDocument(document);
            }
            catch (ArgumentException)
            {
                BackupCorruptFile();
                return CreateDefaultDocument();
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
            MainRecordingSettingsDocument normalized = NormalizeDocument(document);
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

        private static MainRecordingSettingsDocument CreateDefaultDocument()
        {
            return new MainRecordingSettingsDocument();
        }

        private static MainRecordingSettingsDocument NormalizeDocument(MainRecordingSettingsDocument document)
        {
            if (document == null)
            {
                return CreateDefaultDocument();
            }

            if (document.schemaVersion <= 0)
            {
                document.schemaVersion = 1;
            }

            if (document.captureWidth <= 0)
            {
                document.captureWidth = 1920;
            }

            if (document.captureHeight <= 0)
            {
                document.captureHeight = 1080;
            }

            if (document.updatedAtUtc == null)
            {
                document.updatedAtUtc = string.Empty;
            }

            if (document.fbxPath == null)
            {
                document.fbxPath = string.Empty;
            }

            if (document.characterModelPath == null)
            {
                document.characterModelPath = string.Empty;
            }

            if (document.runtimeState == null)
            {
                document.runtimeState = new MainRecordingSettingsState();
            }

            document.runtimeState.Normalize();

            if (document.pendingCommand == null)
            {
                document.pendingCommand = new MainRecordingSettingsCommandEnvelope();
            }

            if (document.pendingCommand.commandId == null)
            {
                document.pendingCommand.commandId = string.Empty;
            }

            if (document.pendingCommand.action == null)
            {
                document.pendingCommand.action = string.Empty;
            }

            if (document.pendingCommand.fbxPath == null)
            {
                document.pendingCommand.fbxPath = string.Empty;
            }

            if (document.pendingCommand.requestedAtUtc == null)
            {
                document.pendingCommand.requestedAtUtc = string.Empty;
            }

            return document;
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
