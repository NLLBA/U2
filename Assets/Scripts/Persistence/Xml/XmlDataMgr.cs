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
    public void Save(object data, string fileName)
    {
        string path = Path.Combine(Application.persistentDataPath, fileName + ".xml");
        SavePath(data, path);
    }

    public void SavePath(object data, string path)
    {
        if (data == null)
            throw new ArgumentNullException("data");

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = path + ".tmp";
        using (StreamWriter writer = new StreamWriter(tempPath, false))
        {
            XmlSerializer serializer = new XmlSerializer(data.GetType());
            serializer.Serialize(writer, data);
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(tempPath, path);
    }

    /// <summary>
    /// 从xml文件中读取内容 
    /// </summary>
    /// <param name="type">对象类型</param>
    /// <param name="fileName">文件名</param>
    /// <returns></returns>
    public object Load(Type type, string fileName)
    {
        string dataPath = Path.Combine(Application.persistentDataPath, fileName + ".xml");
        string defaultPath = Path.Combine(Application.streamingAssetsPath, fileName + ".xml");
        return LoadPaths(type, dataPath, defaultPath);
    }

    public object LoadPaths(Type type, string dataPath, string defaultPath)
    {
        if (type == null)
            throw new ArgumentNullException("type");

        if (File.Exists(dataPath))
        {
            object savedData;
            Exception readError;
            if (TryRead(type, dataPath, out savedData, out readError))
            {
                return savedData;
            }

            string badPath = BackupFile(dataPath);
            Debug.LogWarning("XML data was corrupt and has been backed up to " + badPath + ". " +
                readError.Message);

            object restored = LoadDefault(type, defaultPath);
            SaveCopy(restored, dataPath);
            return restored;
        }

        return LoadDefault(type, defaultPath);
    }

    private object LoadDefault(Type type, string defaultPath)
    {
        if (!string.IsNullOrEmpty(defaultPath) && File.Exists(defaultPath))
        {
            object defaultData;
            Exception defaultError;
            if (TryRead(type, defaultPath, out defaultData, out defaultError))
            {
                return defaultData;
            }

            Debug.LogError("Default XML data could not be loaded: " + defaultPath + ". " +
                defaultError.Message);
        }

        return Activator.CreateInstance(type);
    }

    private bool TryRead(Type type, string path, out object data, out Exception error)
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

    private string BackupFile(string path)
    {
        string directory = Path.GetDirectoryName(path);
        string fileName = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
        string badPath = Path.Combine(directory ?? string.Empty,
            fileName + ".corrupt-" + timestamp + extension);
        int suffix = 1;

        while (File.Exists(badPath))
        {
            badPath = Path.Combine(directory ?? string.Empty,
                fileName + ".corrupt-" + timestamp + "-" + suffix + extension);
            suffix++;
        }

        File.Move(path, badPath);
        return badPath;
    }

    private void SaveCopy(object data, string dataPath)
    {
        try
        {
            SavePath(data, dataPath);
        }
        catch (Exception ex)
        {
            Debug.LogError("Recovered XML data could not be saved: " + dataPath + ". " + ex.Message);
        }
    }
}
