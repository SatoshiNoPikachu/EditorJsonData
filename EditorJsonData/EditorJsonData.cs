using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using LitJson;
using UnityEngine;
using Type = System.Type;

namespace EditorJsonData;

/// <summary>
/// ModEditor JsonData
/// </summary>
public class EditorJsonData
{
    public static void WriteGameJsonData()
    {
        var data = new EditorJsonData(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        var cards = new Dictionary<string, List<object>>
        {
            [CardTypes.Base.ToString()] = new(),
            [CardTypes.Blueprint.ToString()] = new(),
            [CardTypes.Hand.ToString()] = new(),
            [CardTypes.Environment.ToString()] = new(),
            [CardTypes.Event.ToString()] = new(),
            [CardTypes.Explorable.ToString()] = new(),
            [CardTypes.Item.ToString()] = new(),
            [CardTypes.Liquid.ToString()] = new(),
            [CardTypes.Location.ToString()] = new(),
            [CardTypes.Weather.ToString()] = new(),
            [CardTypes.EnvDamage.ToString()] = new(),
            [CardTypes.EnvImprovement.ToString()] = new(),
        };
        foreach (var card in GameLoad.Instance.DataBase.AllData.OfType<CardData>())
        {
            cards[card.CardType.ToString()].Add(card);
        }

        foreach (var type in typeof(CardData).Module.GetTypes())
        {
            if (!type.IsSubclassOf(typeof(ScriptableObject))) continue;
            if (type == typeof(ScriptableObject) || type == typeof(UniqueIDScriptable)) continue;
            data.AddType(type, true);
            if (type == typeof(CardData)) continue;
            data.SetObj(type, Resources.FindObjectsOfTypeAll(type).Cast<object>().ToList());
        }

        data.AddType<Sprite>();
        data.SetObj<Sprite>(Resources.FindObjectsOfTypeAll<Sprite>().Cast<object>().ToList());

        data.AddType<AudioClip>();
        data.SetObj<AudioClip>(Resources.FindObjectsOfTypeAll<AudioClip>().Cast<object>().ToList());

        data.SetObj<CardData>(cards);

        data.CreateJsonData();
    }

    /// <summary>
    /// 类型字典
    /// </summary>
    private readonly Dictionary<Type, string> _types = new();

    /// <summary>
    /// 类型对象字典
    /// </summary>
    private readonly Dictionary<Type, Dictionary<string, List<object>>> _objs = new();

    /// <summary>
    /// 输出路径
    /// </summary>
    private readonly string _output;

    /// <summary>
    /// 前缀
    /// </summary>
    private readonly string _prefix;

    /// <summary>
    /// 实例化 EditorJsonData 对象
    /// </summary>
    /// <param name="output">输出路径</param>
    /// <param name="prefix">前缀</param>
    public EditorJsonData(string output, string prefix = "")
    {
        _output = Path.Combine(output, "CSTI-JsonData");
        _prefix = prefix;
    }

    /// <summary>
    /// 添加类型
    /// </summary>
    /// <param name="type"></param>
    /// <param name="isAutoAddField"></param>
    public void AddType(Type type, bool isAutoAddField)
    {
        AddType(type, "", isAutoAddField);
    }

    /// <summary>
    /// 添加类型
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="name">别名</param>
    /// <param name="isAutoAddField">是否自动添加字段类型</param>
    public void AddType(Type type, string name = "", bool isAutoAddField = false)
    {
        if (name == "") name = type.Name;
        _types[type] = _prefix + name;
        if (!isAutoAddField) return;

        foreach (var t in GetSerializedFields(type).Select(field => ResolveType(field.FieldType))
                     .Where(t => !_types.ContainsKey(t)))
        {
            AddType(t, "", true);
        }
    }

    /// <summary>
    /// 添加类型
    /// </summary>
    /// <param name="isAutoAddField">是否自动添加字段类型</param>
    /// <typeparam name="T">类型</typeparam>
    public void AddType<T>(bool isAutoAddField)
    {
        AddType(typeof(T), "", isAutoAddField);
    }

    /// <summary>
    /// 添加类型
    /// </summary>
    /// <param name="name">别名</param>
    /// <param name="isAutoAddField">是否自动添加字段类型</param>
    /// <typeparam name="T">类型</typeparam>
    public void AddType<T>(string name = "", bool isAutoAddField = false)
    {
        AddType(typeof(T), name, isAutoAddField);
    }

    /// <summary>
    /// 设置对象
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="objs">对象列表</param>
    public void SetObj(Type type, List<object> objs)
    {
        if (_types.ContainsKey(type) && objs is not null)
            _objs[type] = new Dictionary<string, List<object>> { [""] = objs };
    }

    /// <summary>
    /// 设置对象
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="objs">对象字典</param>
    public void SetObj(Type type, Dictionary<string, List<object>> objs)
    {
        if (_types.ContainsKey(type) && objs is not null) _objs[type] = objs;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objs"></param>
    /// <typeparam name="T"></typeparam>
    public void SetObj<T>(List<object> objs)
    {
        SetObj(typeof(T), objs);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objs"></param>
    /// <typeparam name="T"></typeparam>
    public void SetObj<T>(Dictionary<string, List<object>> objs)
    {
        SetObj(typeof(T), objs);
    }

    /// <summary>
    /// 创建 JsonData
    /// </summary>
    public void CreateJsonData()
    {
        Debug.Log("----- Start Create JsonData -----");

        CreateTypeJsonData();
        CreateNotes();
        CreateTemplate();

        Debug.Log("----- End Create JsonData -----");
    }

    /// <summary>
    /// 创建 TypeJsonData
    /// </summary>
    private void CreateTypeJsonData()
    {
        var dir = Directory.CreateDirectory(Path.Combine(_output, "ScriptableObjectTypeJsonData"));

        foreach (var (type, name) in _types)
        {
            if (type.IsEnum)
            {
                CreateEnumTypeJsonData(type, name);
                continue;
            }

            Debug.Log($"Type: {type.Name}");
            var fields = new Dictionary<string, string>();
            foreach (var field in GetSerializedFields(type))
            {
                var field_type = ResolveType(field.FieldType);
                fields[field.Name] = _types.TryGetValue(field_type, out var n) ? n : field_type.Name;
            }

            foreach (var prop in GetSpecialTypeProperty(type))
            {
                var prop_type = ResolveType(prop.PropertyType);
                fields[prop.Name] = _types.TryGetValue(prop_type, out var n) ? n : prop_type.Name;
            }

            OutputJson(dir.FullName, name, JsonMapper.ToJson(fields));
        }
    }

    private static IEnumerable<PropertyInfo> GetSpecialTypeProperty(Type type)
    {
        PropertyInfo[] props = null;

        if (type == typeof(Vector2Int))
            props = new[] { type.GetProperty("x"), type.GetProperty("y") };

        if (type == typeof(Vector3Int))
            props = new[] { type.GetProperty("x"), type.GetProperty("y"), type.GetProperty("z") };

        return props ?? new PropertyInfo[] { };
    }

    /// <summary>
    /// 创建枚举类型 TypeJsonData
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="name">名称</param>
    private void CreateEnumTypeJsonData(Type type, string name)
    {
        Debug.Log($"Enum: {type.Name}");

        var fields = new Dictionary<string, object>();
        foreach (var field in type.GetFields())
        {
            if (field.Name == "value__") continue;
            var desc = field.GetCustomAttribute<DescriptionAttribute>();
            fields[desc is null ? field.Name : desc.Description] = Enum.Parse(type, field.Name);
        }

        var path = Path.Combine(_output, "ScriptableObjectTypeJsonData", "EnumType");
        var dir = Directory.CreateDirectory(path);
        OutputJson(dir.FullName, name, JsonMapper.ToJson(fields));
    }

    /// <summary>
    /// 创建注释
    /// </summary>
    private void CreateNotes()
    {
        var dir_zh = Directory.CreateDirectory(Path.Combine(_output, "Notes"));
        var dir_en = Directory.CreateDirectory(Path.Combine(_output, "Notes-En"));

        foreach (var (type, name) in _types)
        {
            if (type.IsEnum) continue;

            Debug.Log($"Notes: {type.Name}");

            var zh = new FileInfo(Path.Combine(dir_zh.FullName, $"{name}.txt"));
            var en = new FileInfo(Path.Combine(dir_en.FullName, $"{name}.txt"));

            var dict_zh = LoadNotes(zh.FullName);
            var dict_en = LoadNotes(en.FullName);

            var fields = (from field in GetSerializedFields(type) select field.Name).ToList();

            var f_zh = zh.CreateText();
            var f_en = en.CreateText();
            foreach (var field in fields)
            {
                var n_zh = dict_zh.TryGetValue(field, out var v1) ? v1 : "";
                var n_en = dict_en.TryGetValue(field, out var v2) ? v2 : "";
                f_zh.WriteLine($"{field}\t{n_zh}");
                f_en.WriteLine($"{field}\t{n_en}");
            }

            f_zh.Flush();
            f_zh.Close();
            f_en.Flush();
            f_en.Close();
        }
    }

    /// <summary>
    /// 加载注释
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns>注释字典</returns>
    private static Dictionary<string, string> LoadNotes(string path)
    {
        var dict = new Dictionary<string, string>();
        if (!File.Exists(path)) return dict;

        var lines = File.ReadAllLines(path, Encoding.UTF8);
        foreach (var line in lines)
        {
            if (line == "") continue;

            var data = line.Split(new char[] { '\t' }, 2);
            dict[data[0]] = data.Length == 1 ? "" : data[1];
        }

        return dict;
    }

    /// <summary>
    /// 创建模板
    /// </summary>
    private void CreateTemplate()
    {
        var dir_tmp = Directory.CreateDirectory(Path.Combine(_output, "ScriptableObjectJsonDataWithWarpLitAllInOne"));
        var dir_name = Directory.CreateDirectory(Path.Combine(_output, "ScriptableObjectObjectName"));
        var dir_base = Directory.CreateDirectory(Path.Combine(_output, "UniqueIDScriptableBaseJsonData"));

        foreach (var (type, name) in _types)
        {
            if (!type.IsSubclassOf(typeof(UnityEngine.Object))) continue;

            Debug.Log($"Template: {type.Name}");

            var data = TypeToJsonData(type, name);
            var json = data.ToJson();

            if (type.IsSubclassOf(typeof(UniqueIDScriptable)))
            {
                OutputJson(dir_base.FullName, name, json);
                UidObjToTemplate(type);
                continue;
            }

            if (type.IsSubclassOf(typeof(ScriptableObject)))
            {
                var dir = Directory.CreateDirectory(Path.Combine(dir_tmp.FullName, name));
                OutputJson(dir.FullName, "", json);
                OutputJson(dir.FullName, "BaseTemplate(模板)", json);
                OutputTxt(dir_name.FullName, name, "BaseTemplate(模板)");
            }

            ObjToTemplate(type);
        }
    }

    /// <summary>
    /// Object 对象转模板
    /// </summary>
    /// <param name="type">类型</param>
    private void ObjToTemplate(Type type)
    {
        if (!type.IsSubclassOf(typeof(UnityEngine.Object))) return;
        if (!_objs.ContainsKey(type)) return;

        var dir_name = Directory.CreateDirectory(Path.Combine(_output, "ScriptableObjectObjectName"));

        var list_name = new List<string>();

        foreach (var (sub, objs) in _objs[type])
        {
            if (sub != "" || objs is null) continue;

            foreach (var obj in objs)
            {
                if (obj is not UnityEngine.Object o) continue;
                var name = o.name;
                if (name == "") continue;
                list_name.Add(name);
                if (o is not ScriptableObject) continue;
                var dir_tmp = Directory.CreateDirectory(Path.Combine(_output,
                    "ScriptableObjectJsonDataWithWarpLitAllInOne", _types[type]));
                OutputJson(dir_tmp.FullName, name, ObjToJson(obj));
            }
        }

        OutputTxt(dir_name.FullName, _types[type], list_name, true);
    }

    /// <summary>
    /// UniqueIDScriptable 对象转模板
    /// </summary>
    /// <param name="type">类型</param>
    private void UidObjToTemplate(Type type)
    {
        if (!type.IsSubclassOf(typeof(UniqueIDScriptable))) return;
        if (!_objs.ContainsKey(type)) return;

        var dir_uid = Directory.CreateDirectory(Path.Combine(_output, "UniqueIDScriptableGUID"));
        var dir_tmp =
            Directory.CreateDirectory(Path.Combine(_output, "UniqueIDScriptableJsonDataWithWarpLitAllInOne",
                _types[type]));

        var dict_uid = new Dictionary<string, Dictionary<string, string>>();

        var field_name = GetTypeGameNameField(type);
        foreach (var (sub, objs) in _objs[type])
        {
            var dir = sub == "" ? dir_tmp : Directory.CreateDirectory(Path.Combine(dir_tmp.FullName, sub));
            if (!dict_uid.ContainsKey(sub)) dict_uid[sub] = new Dictionary<string, string>();
            if (objs is null) continue;

            foreach (var obj in objs)
            {
                if (!obj.GetType().IsSubclassOf(typeof(UniqueIDScriptable))) continue;

                var uid_obj = (UniqueIDScriptable)obj;
                var key = uid_obj.name;
                if (field_name is not null)
                {
                    var n = ((LocalizedString)field_name.GetValue(uid_obj)).ToString();
                    if (n != "") key += $"({n})";
                }

                dict_uid[sub][key] = uid_obj.UniqueID;

                OutputJson(dir.FullName, uid_obj.name, ObjToJson(obj));
            }
        }

        foreach (var (sub, dt) in dict_uid)
        {
            if (sub == "")
            {
                OutputJson(dir_uid.FullName, _types[type], JsonMapper.ToJson(dt));
                continue;
            }

            var dir_uid_sub = Directory.CreateDirectory(Path.Combine(dir_uid.FullName, _types[type]));
            OutputJson(dir_uid_sub.FullName, sub, JsonMapper.ToJson(dt));
        }
    }

    /// <summary>
    /// 获取类型的游戏内名称字段
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>字段</returns>
    private static FieldInfo GetTypeGameNameField(Type type)
    {
        FieldInfo field = null;

        if (type == typeof(BookmarkGroup))
            field = typeof(BookmarkGroup).GetField("GroupName");

        else if (type == typeof(CardData))
            field = typeof(CardData).GetField("CardName");

        else if (type == typeof(CharacterPerk))
            field = typeof(CharacterPerk).GetField("PerkName");

        else if (type == typeof(Encounter))
            field = typeof(Encounter).GetField("EncounterTitle");

        else if (type == typeof(GameModifierPackage))
            field = typeof(GameModifierPackage).GetField("PackageName");

        else if (type == typeof(GameStat))
            field = typeof(GameStat).GetField("GameName");

        else if (type == typeof(Objective))
            field = typeof(Objective).GetField("ObjectiveDescription");

        else if (type == typeof(PerkGroup))
            field = typeof(PerkGroup).GetField("GroupName");

        else if (type == typeof(PerkTabGroup))
            field = typeof(PerkTabGroup).GetField("TabName");

        else if (type == typeof(PlayerCharacter))
            field = typeof(PlayerCharacter).GetField("CharacterName");

        return field;
    }

    /// <summary>
    /// 对象转 Json
    /// </summary>
    /// <param name="obj">对象</param>
    /// <param name="data">Json数据</param>
    /// <param name="isToJson">是否转成Json</param>
    /// <returns>Json字符串</returns>
    private static string ObjToJson(object obj, JsonData data = null, bool isToJson = true)
    {
        data ??= JsonMapper.ToObject(JsonUtility.ToJson(obj));
        var type = obj.GetType();
        foreach (var field in GetSerializedFields(type))
        {
            var field_type = field.FieldType;
            if (field_type.IsPrimitive || field_type.IsEnum || field_type == typeof(string)) continue;

            var field_value = field.GetValue(obj);
            if (typeof(IList).IsAssignableFrom(field_type))
            {
                var main = new JsonData();
                var warp = new JsonData();
                main.SetJsonType(JsonType.Array);
                warp.SetJsonType(JsonType.Array);

                if (field_value is not null)
                {
                    foreach (var element in (IList)field_value)
                    {
                        if (element is null)
                        {
                            var t = ResolveType(field_type);
                            if (t.IsSubclassOf(typeof(UnityEngine.Object)))
                            {
                                main.Add(new JsonData
                                {
                                    ["m_FileID"] = 0,
                                    ["m_PathID"] = 0
                                });
                            }

                            continue;
                        }

                        if (main.Count == 0 && element.GetType().IsSubclassOf(typeof(UnityEngine.Object)))
                        {
                            if (element is UniqueIDScriptable uidObj) warp.Add(uidObj.UniqueID);
                            else warp.Add(((UnityEngine.Object)element).name);
                        }
                        else if (warp.Count == 0)
                        {
                            var element_data = JsonMapper.ToObject(JsonUtility.ToJson(element));
                            ObjToJson(element, element_data, false);
                            main.Add(element_data);
                        }
                    }
                }

                data[field.Name] = main;
                if (warp.Count == 0) continue;
                data[$"{field.Name}WarpData"] = warp;
                data[$"{field.Name}WarpType"] = 3;
            }
            else if (field_type.IsSubclassOf(typeof(UnityEngine.Object)))
            {
                data[field.Name] = new JsonData
                {
                    ["m_FileID"] = 0,
                    ["m_PathID"] = 0
                };
                if (field_value is null) continue;

                data[$"{field.Name}WarpData"] = field_value is UniqueIDScriptable uid_obj
                    ? uid_obj.UniqueID
                    : ((UnityEngine.Object)field_value).name;
                data[$"{field.Name}WarpType"] = 3;
            }
            else
            {
                if (!data.ContainsKey(field.Name) && field_value is not null)
                    data[field.Name] = JsonMapper.ToObject(JsonUtility.ToJson(field_value));
                if (field_value is not null)
                    ObjToJson(field_value, data[field.Name], false);
                // else
                //     data[field.Name] = new JsonData(null);
            }
        }

        return isToJson ? data.ToJson() : "";
    }

    /// <summary>
    /// 创建 BaseJson (UniqueIDScriptableBaseJsonData)
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="base_name">顶层类型名称</param>
    private void CreateBaseJson(Type type, string base_name)
    {
        // if (type.IsArray) type = type.GetElementType();
        // if (type is null) return;
        type = ResolveType(type);

        if (type.IsEnum) return;
        if (type.IsPrimitive || type == typeof(string)) return;
        if (!_types.ContainsKey(type)) return;
        if (type.IsSubclassOf(typeof(UnityEngine.Object))) return;

        var dir = Directory.CreateDirectory(Path.Combine(_output, "UniqueIDScriptableBaseJsonData", base_name));
        if (File.Exists(Path.Combine(dir.FullName, $"{_types[type]}.json"))) return;

        Debug.Log($"-- BaseJson: {type.Name}");
        var data = TypeToJsonData(type, base_name);
        OutputJson(dir.FullName, _types[type], data.ToJson());
    }

    /// <summary>
    /// 类型转 JsonData
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="base_name">顶层类型名称</param>
    /// <returns>JsonData</returns>
    private JsonData TypeToJsonData(Type type, string base_name)
    {
        var data = new JsonData();

        bool is_create;
        if (_types[type] == base_name)
        {
            // is_create = type.IsSubclassOf(typeof(UniqueIDScriptable)) || type.IsSubclassOf(typeof(ScriptableObject)) &&
            //     type.Module != typeof(CardData).Module;
            is_create = type.IsSubclassOf(typeof(ScriptableObject));
        }
        else
        {
            is_create = !type.IsSubclassOf(typeof(UniqueIDScriptable));
        }

        var no_field = true;
        var is_script = type.IsSubclassOf(typeof(ScriptableObject)) && !type.IsSubclassOf(typeof(UniqueIDScriptable));
        foreach (var field in GetSerializedFields(type))
        {
            no_field = false;
            if (is_create) CreateBaseJson(field.FieldType, is_script ? "FromScriptableObject" : base_name);
            data[field.Name] = FieldToJsonData(field, base_name);
        }

        if (no_field) data.SetJsonType(JsonType.Object);
        return data;
    }

    /// <summary>
    /// 字段转 JsonData
    /// </summary>
    /// <param name="field">字段信息</param>
    /// <param name="base_name">顶层类型名称</param>
    /// <returns>JsonData</returns>
    private JsonData FieldToJsonData(FieldInfo field, string base_name)
    {
        JsonData data;
        var type = field.FieldType;

        if (type.IsPrimitive)
        {
            if (type == typeof(bool)) data = new JsonData(false);
            else if (type == typeof(char)) data = new JsonData("");
            else if (type == typeof(float) || type == typeof(double)) data = new JsonData(0.0);
            else data = new JsonData(0);
        }
        else if (type == typeof(string))
        {
            data = new JsonData("");
        }
        else if (type.IsEnum)
        {
            data = new JsonData(0);
        }
        else if (type.IsArray || ResolveGenericType(type) == typeof(List<>))
        {
            data = new JsonData();
            data.SetJsonType(JsonType.Array);
        }
        else
        {
            data = ClassFieldToJsonData(type, base_name);
        }

        return data;
    }

    /// <summary>
    /// 类类型转 JsonData
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="base_name">顶层类型名称</param>
    /// <returns>JsonData</returns>
    private JsonData ClassFieldToJsonData(Type type, string base_name)
    {
        if (!type.IsSubclassOf(typeof(UnityEngine.Object))) return TypeToJsonData(type, base_name);

        var data = new JsonData
        {
            ["m_FileID"] = 0,
            ["m_PathID"] = 0
        };
        return data;
    }

    /// <summary>
    /// 输出 Json 格式文件
    /// </summary>
    /// <param name="path">输出路径</param>
    /// <param name="name">文件名称</param>
    /// <param name="json">Json字符串</param>
    private static void OutputJson(string path, string name, string json)
    {
        try
        {
            json = Regex.Unescape(json);
        }
        catch (ArgumentException)
        {
        }

        var file = File.CreateText(Path.Combine(path, $"{name}.json"));
        file.WriteLine(json);
        file.Flush();
        file.Close();
    }

    /// <summary>
    /// 输出 txt 格式文件
    /// </summary>
    /// <param name="path">输出路径</param>
    /// <param name="name">文件名称</param>
    /// <param name="text">文件内容</param>
    private static void OutputTxt(string path, string name, string text)
    {
        var file = File.CreateText(Path.Combine(path, $"{name}.txt"));
        file.WriteLine(text);
        file.Flush();
        file.Close();
    }

    /// <summary>
    /// 输出 txt 格式文件
    /// </summary>
    /// <param name="path">输出路径</param>
    /// <param name="name">文件名称</param>
    /// <param name="lines">行列表</param>
    /// <param name="isAppend">是否追加</param>
    private static void OutputTxt(string path, string name, List<string> lines, bool isAppend = false)
    {
        var file = isAppend
            ? File.AppendText(Path.Combine(path, $"{name}.txt"))
            : File.CreateText(Path.Combine(path, $"{name}.txt"));

        foreach (var line in lines) file.WriteLine(line);
        file.Flush();
        file.Close();
    }

    /// <summary>
    /// 解析类型 <br/>
    /// 若参数 type 不满足以下任一情况，则直接返回该参数 <br/>
    /// 为整数类型时，返回 int 类型 <br/>
    /// 为字符类型时，返回 string 类型 <br/>
    /// 为数组类型时，返回数组元素的类型 <br/>
    /// 为列表类型时，返回列表元素的类型
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>解析后的类型</returns>
    private static Type ResolveType(Type type)
    {
        var t = type;

        // 解析数组类型
        if (type.IsArray) t = type.GetElementType();
        if (t is null) return type;

        // 解析列表类型
        if (ResolveGenericType(type) == typeof(List<>)) t = type.GetGenericArguments()[0];

        // 解析基元类型
        if (!t.IsPrimitive) return t;
        if (t == typeof(char)) return typeof(string);
        if (t != typeof(bool) && t != typeof(float) && t != typeof(double)) return typeof(int);

        return t;
    }

    /// <summary>
    /// 解析泛型类型
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>解析后的类型</returns>
    private static Type ResolveGenericType(Type type)
    {
        return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
    }

    // /// <summary>
    // /// 字段是否不可序列化 <br/>
    // /// 不可序列化的字段：字典类型，或 static | const | readonly 修饰的
    // /// </summary>
    // /// <param name="field">字段</param>
    // /// <returns>是否不可序列化</returns>
    // private static bool IsFieldNotSerialized(FieldInfo field)
    // {
    //     if (field.IsStatic) return true;
    //     if (field.IsLiteral) return true;
    //     if (field.IsInitOnly) return true;
    //     if (ResolveGenericType(field.FieldType) == typeof(Dictionary<,>)) return true;
    //     // if (!field.FieldType.IsSubclassOf(typeof(UnityEngine.Object)) &&
    //     //     field.FieldType.GetCustomAttribute<SerializableAttribute>() is null) return true;
    //
    //     return field.GetCustomAttribute<NonSerializedAttribute>() is not null;
    // }

    /// <summary>
    /// 字段是否可序列化
    /// </summary>
    /// <param name="field">字段</param>
    /// <returns>是否可序列化</returns>
    private static bool IsFieldSerialized(FieldInfo field)
    {
        if (field.IsStatic) return false;
        if (field.IsLiteral) return false;
        if (field.IsInitOnly) return false;
        if (ResolveGenericType(field.FieldType) == typeof(Dictionary<,>)) return false;
        if (field.GetCustomAttribute<NonSerializedAttribute>() is not null) return false;

        return field.IsPublic || field.GetCustomAttribute<SerializeField>() is not null;
    }


    /// <summary>
    /// 获取类型所有可序列化的字段
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>可序列化字段列表</returns>
    private static List<FieldInfo> GetSerializedFields(IReflect type)
    {
        return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(IsFieldSerialized).ToList();
    }
}