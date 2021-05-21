﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;

namespace HotUnity.Editor
{
    [CustomEditor(typeof(HotScriptAdapter))]
    public class HotScriptAdapterEditor : UnityEditor.Editor
    {
        private ILRuntime.Runtime.Enviorment.AppDomain hotAssembly => assemblyLoader.appdomain;
        private HotAssemblyLoader assemblyLoader;
        private new HotScriptAdapter target => serializedObject.targetObject as HotScriptAdapter;

        private void OnEnable()
        {
            assemblyLoader = assemblyLoader ?? new HotAssemblyLoader();
            assemblyLoader.Reloead();
        }

        private void OnDisable()
        {
            assemblyLoader?.Unload();
        }

        protected override void OnHeaderGUI()
        {
            var rect = EditorGUILayout.GetControlRect(false, 0f);
            rect.height = EditorGUIUtility.singleLineHeight;
            rect.y -= rect.height;

            // icon
            rect.x = 16;
            rect.xMax = 32;
            EditorGUI.DrawRect(rect, Helper.backgroudColor);
            GUI.DrawTexture(rect, Helper.scriptIcon);

            // title
            rect.x = 48;
            rect.xMax = EditorGUIUtility.currentViewWidth - 96;
            EditorGUI.DrawRect(rect, Helper.backgroudColor);
            var className = target.targetClass;
            if (className.LastIndexOf('.') != -1)
            {
                className = className.Substring(className.LastIndexOf('.') + 1);
            }
            string header = $"{className} (HotScript)";
            EditorGUI.LabelField(rect, header, EditorStyles.boldLabel);
        }

        public override void OnInspectorGUI()
        {
            OnHeaderGUI();

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            bool isEditorMode = prefabStage != null || !Application.isPlaying;

            var type = hotAssembly.GetType(target.targetClass)?.ReflectionType;
            if (type == null)
            {
                target.targetClass = EditorGUILayout.TextField("Target Class", target.targetClass);
                EditorGUILayout.HelpBox($"Target Class Not Found: {target.targetClass}, You can fix it manually.",
                    MessageType.Error);
                return;
            }

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            if (isEditorMode)
            {
                target.cacheInfos = target.cacheInfos ?? new HotScriptAdapter.CacheInfo[0];
                var infos = new List<HotScriptAdapter.CacheInfo>(target.cacheInfos);
                bool changed = false;
                foreach (var f in fields)
                {
                    changed |= EditorDrawField(f, infos);
                }
                changed |= infos.Count != target.cacheInfos.Length;
                for (int i = infos.Count - 1; i >= 0; i--)
                {
                    if (!fields.Any(a => a.Name == infos[i].fieldName &&
                    a.FieldType.FullName == infos[i].typeName))
                    {
                        changed = true;
                        infos.RemoveAt(i);
                    }
                }
                if (changed)
                {
                    target.cacheInfos = infos.ToArray();
                    EditorUtility.SetDirty(target);
                }
            }
            else
            {
                foreach (var f in fields)
                {
                    RuntimeDrawField(f, target.targetObj);
                }
            }
        }

        private HotScriptAdapter.CacheInfo EditorDrawInfo(Type type, HotScriptAdapter.CacheInfo info)
        {
            var title = Helper.ToTitle(info.fieldName);
            if (info.typeName == typeof(string).FullName)
            {
                info.stringValue = EditorGUILayout.TextField(title, info.stringValue);
            }
            else if (info.typeName == typeof(Vector3).FullName)
            {
                info.vector3Value = EditorGUILayout.Vector3Field(title, info.vector3Value);
            }
            else if (type.IsClass && typeof(Component).IsAssignableFrom(type))
            {
                info.componentValue = (Component)EditorGUILayout.ObjectField(title, info.componentValue, type);
            }
            else if (type.IsClass &&
                hotAssembly.GetType("HotUnity.HotScript").
                ReflectionType.IsAssignableFrom(type))
            {
                var tempComp = (Component)EditorGUILayout.ObjectField(title, info.componentValue, typeof(HotScriptAdapter));
                if (tempComp != null && tempComp is HotScriptAdapter
                    && ((HotScriptAdapter)tempComp).targetClass == type.FullName)
                {
                    info.componentValue = tempComp;
                }
                else
                {
                    info.componentValue = null;
                }
            }
            return info;
        }

        private bool EditorDrawField(FieldInfo fieldInfo, List<HotScriptAdapter.CacheInfo> infos)
        {
            var index = infos.FindIndex(a =>
            {
                return a.fieldName == fieldInfo.Name &&
                a.typeName == fieldInfo.FieldType.FullName;
            });

            HotScriptAdapter.CacheInfo info;
            if (index == -1)
            {
                info = new HotScriptAdapter.CacheInfo();
                info.fieldName = fieldInfo.Name;
                info.typeName = fieldInfo.FieldType.FullName;
                infos.Add(info);
                index = infos.Count - 1;
            }
            else
            {
                info = infos[index];
            }

            info = EditorDrawInfo(fieldInfo.FieldType, info);

            if (!infos[index].Equals(info))
            {
                infos[index] = info;
                return true;
            }
            return false;
        }

        private void RuntimeDrawField(FieldInfo fieldInfo, object obj)
        {
            var title = Helper.ToTitle(fieldInfo.Name);
            if (fieldInfo.FieldType.FullName == typeof(string).FullName)
            {
                var value = EditorGUILayout.TextField(title, $"{fieldInfo.GetValue(obj)}");
                fieldInfo.SetValue(obj, value);
            }
            else if (fieldInfo.FieldType.FullName == typeof(Vector3).FullName)
            {
                var value = EditorGUILayout.Vector3Field(title, (Vector3)fieldInfo.GetValue(obj));
                fieldInfo.SetValue(obj, value);
            }
        }
    }

}
