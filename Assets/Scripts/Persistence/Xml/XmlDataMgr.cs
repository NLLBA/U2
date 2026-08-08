using System;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

public class XmlDataMgr
{
    private static XmlDataMgr instance = new XmlDataMgr();

    public static XmlDataMgr Instance => instance;

    private XmlDataMgr() { }

    /// <summary>
    /// 保存数据到xml文件中
    /// </summary>
    /// <param name="data">数据对象</param>
    /// <param name="fileName">文件名</param>
    public void SaveData(object data, string fileName)
    {
        string path = Path.Combine(Application.persistentDataPath, fileName + ".xml");
        SaveDataToPath(data, path);
    }

    public void SaveDataToPath(object data, string path)
    {
        if (data == null)
            throw new ArgumentNullException("data");

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = path + ".tmp";
        using (StreamWriter writer = new StreamWriter(temporaryPath, false))
        {
            XmlSerializer serializer = new XmlSerializer(data.GetType());
            serializer.Serialize(writer, data);
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(temporaryPath, path);
    }

    /// <summary>
    /// 从xml文件中读取内容 
    /// </summary>
    /// <param name="type">对象类型</param>
    /// <param name="fileName">文件名</param>
    /// <returns></returns>
    public object LoadData(Type type, string fileName)
    {
        string persistentPath = Path.Combine(Application.persistentDataPath, fileName + ".xml");
        string fallbackPath = Path.Combine(Application.streamingAssetsPath, fileName + ".xml");
        return LoadDataFromPaths(type, persistentPath, fallbackPath);
    }

    public object LoadDataFromPaths(Type type, string persistentPath, string fallbackPath)
    {
        if (type == null)
            throw new ArgumentNullException("type");

        if (File.Exists(persistentPath))
        {
            object persistentData;
            Exception persistentError;
            if (TryDeserialize(type, persistentPath, out persistentData, out persistentError))
            {
                return persistentData;
            }

            string backupPath = BackupCorruptFile(persistentPath);
            Debug.LogWarning("XML data was corrupt and has been backed up to " + backupPath + ". " +
                persistentError.Message);

            object recoveredData = LoadFallbackOrDefault(type, fallbackPath);
            TrySaveRecoveredData(recoveredData, persistentPath);
            return recoveredData;
        }

        return LoadFallbackOrDefault(type, fallbackPath);
    }

    private object LoadFallbackOrDefault(Type type, string fallbackPath)
    {
        if (!string.IsNullOrEmpty(fallbackPath) && File.Exists(fallbackPath))
        {
            object fallbackData;
            Exception fallbackError;
            if (TryDeserialize(type, fallbackPath, out fallbackData, out fallbackError))
            {
                return fallbackData;
            }

            Debug.LogError("Default XML data could not be loaded: " + fallbackPath + ". " +
                fallbackError.Message);
        }

        return Activator.CreateInstance(type);
    }

    private bool TryDeserialize(Type type, string path, out object data, out Exception error)
    {
        try
        {
            using (StreamReader reader = new StreamReader(path))
            {
                XmlSerializer serializer = new XmlSerializer(type);
                data = serializer.Deserialize(reader);
                error = null;
                return data != null;
            }
        }
        catch (Exception ex)
        {
            data = null;
            error = ex;
            return false;
        }
    }

    private string BackupCorruptFile(string path)
    {
        string directory = Path.GetDirectoryName(path);
        string fileName = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
        string backupPath = Path.Combine(directory ?? string.Empty,
            fileName + ".corrupt-" + timestamp + extension);
        int suffix = 1;

        while (File.Exists(backupPath))
        {
            backupPath = Path.Combine(directory ?? string.Empty,
                fileName + ".corrupt-" + timestamp + "-" + suffix + extension);
            suffix++;
        }

        File.Move(path, backupPath);
        return backupPath;
    }

    private void TrySaveRecoveredData(object data, string persistentPath)
    {
        try
        {
            SaveDataToPath(data, persistentPath);
        }
        catch (Exception ex)
        {
            Debug.LogError("Recovered XML data could not be saved: " + persistentPath + ". " + ex.Message);
        }
    }
}
