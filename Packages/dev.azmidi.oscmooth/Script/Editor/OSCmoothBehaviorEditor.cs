﻿using System;
using System.Collections.Generic;
using OSCmooth;
using OSCmooth.Editor;
using static OSCmooth.Util.ParameterExtensions;
using OSCmooth.Types;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using OSCmooth.Editor.Animation;

[CustomEditor(typeof(OSCmoothBehavior))]
public class OSCmoothBehaviorEditor : Editor
{
    private OSCmoothSetup _setup;
    private VRCAvatarDescriptor _avatarDescriptor;

    private int _oscmUsage;
    private bool _showGlobalConfiguration = false;
    private bool _settings;

    private bool _debugShow = true;
    private bool _advancedSettings = false;
    private bool _debug;

    private readonly string[] binarySizeOptions = new string[8] 
    { 
        "OFF", 
        "2 (1 Bit)", 
        "4 (2 Bit)", 
        "8 (3 Bit)", 
        "16 (4 Bit)", 
        "32 (5 Bit)", 
        "64 (6 Bit)", 
        "128 (7 Bit)" 
    };

    public override void OnInspectorGUI()
    {
        OSCmoothBehavior _target = (OSCmoothBehavior)base.target;
        if (_target.setup == null)
            _target.setup = new OSCmoothSetup();
        _setup = _target.setup;
        _avatarDescriptor = ((OSCmoothBehavior)base.target).GetComponent<VRCAvatarDescriptor>();

        DrawSettingsSection();
        DrawAnimationLayerSelection(_avatarDescriptor);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        DrawConfigurationSection();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        DrawGlobalConfigurationSection();
        DrawParameterUsageSection();
        _target.setup = _setup;
    }

    private void DrawSettingsSection()
    {
        EditorGUILayout.Space(10f);
        _settings = EditorGUILayout.Foldout(_settings, "Settings");
        if (_settings)
        {
            _debugShow = EditorGUILayout.Toggle(
                new GUIContent("Show Debug Options", 
                               "Shows debug options."), 
                               _debugShow);

            _setup.useBinaryDecoding = EditorGUILayout.Toggle(
                new GUIContent("Binary Parameters", 
                               "Shows Binary Parameter options for parameter configuration."), 
                               _setup.useBinaryDecoding);

            if (_setup.useBinaryDecoding)
            {
                _setup.useBinaryEncoding = EditorGUILayout.Toggle(
                new GUIContent("(Experimental) Binary Parameter Encoder", 
                               "(Experimental) Allows binary parameters to be encoded directly in the animator.\n\n" +
                               "NOTE: This will only be run locally on the avatar, and should not affect performance of remote users."), 
                               _setup.useBinaryEncoding);
            }
        }

        if (!_debugShow)
            return;

        _debug = EditorGUILayout.Foldout(_debug, "Debug");

        if (_debug)
        {
            _advancedSettings = EditorGUILayout.Toggle(
                new GUIContent("Advanced Options", 
                               "Enables more sophsticated options that are usually expoded for internal use."), 
                               _advancedSettings);

            if (GUILayout.Button(
                new GUIContent("Clean Baked OSCmooth from Avatar", 
                               "Removes older, manually setup OSCmooth animation setups from avatars. " +
                               "Use this if you want to non-destructively add OSCmooth using this Behavior script."),
                Array.Empty<GUILayoutOption>()))
            {
                foreach (var layer in _avatarDescriptor.baseAnimationLayers)
                {
                    var _animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(layer.animatorController));
                    if (_animatorController == null) continue;
                    new OSCmoothAnimationHandler
                    (
                        _setup,
                        new VRCAvatarDescriptor.CustomAnimLayer[0]
                    )
                    .RemoveAllOSCmoothFromController();
                }
                AssetDatabase.SaveAssets();
            }
            if (GUILayout.Button(
                new GUIContent("Remove Expression Parameters", 
                               "Removes listed OSCmooth parameters from the avatar's Expression Parameters list so " +
                               "that OSCmooth can manage the listed parameters (such as creating binary parameters)."), 
                Array.Empty<GUILayoutOption>()))
            {
                _avatarDescriptor.RemoveOSCmParameters(_setup.parameters);
                AssetDatabase.SaveAssets();
            }
            if (GUILayout.Button(
                new GUIContent("Test Expression Parameters", 
                               "Adds listed OSCmooth parameters from the avatar's Expression Parameters list so that " +
                               "OSCmooth can manage them (such as creating binary parameters)."), 
                Array.Empty<GUILayoutOption>()))
            {
                _setup.CreateExpressionParameters(_avatarDescriptor, $"Assets/OSCmooth/Temp/{((OSCmoothBehavior)target).gameObject.name}/");
                AssetDatabase.SaveAssets();
            }
            if (GUILayout.Button(
                new GUIContent("Preprocess OSCmooth Directly", 
                               "Creates OSCmooth layers manually. Creates new Animator Controllers and sets up the avatar to use them."), 
                Array.Empty<GUILayoutOption>()))
            {
                new OSCmoothPreprocessor().OnPreprocessAvatar(((OSCmoothBehavior)target).gameObject);
                AssetDatabase.SaveAssets();
            }
            if (GUILayout.Button(
                new GUIContent("Postprocess OSCmooth Directly", 
                               "Reverts OSCmooth setup from avatar. Requires"), 
                Array.Empty<GUILayoutOption>()))
            {
                //new OSCmoothPostprocessor().OnPostprocessAvatar();
                AssetDatabase.SaveAssets();
            }
        }
    }

    private void DrawAnimationLayerSelection(VRCAvatarDescriptor avatarDescriptor)
    {
        if (GUILayout.Button(
            new GUIContent("Use Playable Layer Parameters", 
                           "Populates the parameter list with existing float parameters in all available Playable Layers on your avatar."), 
            Array.Empty<GUILayoutOption>()))
        {
            List<OSCmoothParameter> _layerParameters = new List<OSCmoothParameter>();

            for (int i = 0; i < avatarDescriptor.baseAnimationLayers.Length; i++)
            {
                List<OSCmoothParameter> _layerOSCmParameters = new List<OSCmoothParameter>();

                AnimatorController animatorController = 
                    AssetDatabase.LoadAssetAtPath<AnimatorController>(
                        AssetDatabase.GetAssetPath(avatarDescriptor.baseAnimationLayers[i].animatorController));

                if (animatorController == null)
                    continue;

                AnimatorControllerParameter[] parameters = animatorController.parameters;
                AnimLayerTypeMask layerMask = Extensions.Mask(avatarDescriptor.baseAnimationLayers[i].type);

                foreach (AnimatorControllerParameter parameter in parameters)
                {
                    OSCmoothParameter existingParam = _layerParameters.Find(p => p.paramName == parameter.name);
                    if (existingParam != null)
                    {
                        existingParam.layerMask |= layerMask;
                        continue;
                    }
                    OSCmoothParameter oscmParam = new OSCmoothParameter();
                    oscmParam.paramName = parameter.name;
                    oscmParam.layerMask = layerMask;
                    oscmParam.binarySizeSelection = _setup.configParam.binarySizeSelection;
                    oscmParam.binaryEncoding = _setup.useBinaryEncoding ? _setup.configParam.binaryEncoding : OSCmoothParserType.None;
                    oscmParam.localSmoothness = _setup.configParam.localSmoothness;
                    oscmParam.remoteSmoothness = _setup.configParam.remoteSmoothness;
                    oscmParam.binaryNegative = _setup.configParam.binaryNegative;
                    if (_advancedSettings)
                        oscmParam.convertToProxy = _setup.configParam.convertToProxy;
                    else oscmParam.convertToProxy = true;
                    oscmParam.isVisible = false;
                    
                    _layerParameters.Add(oscmParam);
                }
            }
            _setup.parameters = _layerParameters;
            AssetDatabase.SaveAssets();
        }
        EditorGUILayout.Space();
    }

    private void DrawConfigurationSection()
    {
        _showGlobalConfiguration = EditorGUILayout.Foldout(_showGlobalConfiguration, "Default Parameter Values");
        if (_showGlobalConfiguration)
        {
            DrawParameterConfiguration(_setup.configParam);
        }
    }

    private void DrawGlobalConfigurationSection()
    {
        DrawParameterList();
    }

    private void DrawParameterList()
    {
        for (int i = 0; i < _setup.parameters.Count; i++)
        {
            EditorGUI.indentLevel = 0;
            EditorGUILayout.HorizontalScope horizontalScope = new EditorGUILayout.HorizontalScope();
            try
            {
                if (GUILayout.Button(_setup.parameters[i].isVisible ? "v" : ">", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(20f) }))
                {
                    _setup.parameters[i].isVisible = !_setup.parameters[i].isVisible;
                }
                EditorGUI.BeginChangeCheck();
                string paramName = _setup.parameters[i].paramName;
                paramName = EditorGUILayout.TextField(paramName);
                if (EditorGUI.EndChangeCheck() && _setup.parameters[i] != null)
                {
                    _setup.parameters[i].paramName = paramName;
                    continue;
                }
                GUI.color = Color.red;
                if (GUILayout.Button("X", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(40f) }))
                {
                    _setup.parameters.Remove(_setup.parameters[i]);
                    break;
                }
                GUI.color = Color.white;
            }
            finally
            {
                ((IDisposable)horizontalScope)?.Dispose();
            }
            EditorGUI.indentLevel = 2;
            if (_setup.parameters[i].isVisible)
            {
                DrawParameterConfiguration(_setup.parameters[i]);
            }
        }
        EditorGUILayout.Space();
        if (GUILayout.Button(new GUIContent("Add New Parameter", "Adds a new Parameter configuration."), Array.Empty<GUILayoutOption>()))
        {
            OSCmoothParameter _configParam = _setup.configParam;
            OSCmoothParameter param = new OSCmoothParameter
            {
                paramName = _configParam.paramName,
                localSmoothness = _configParam.localSmoothness,
                remoteSmoothness = _configParam.remoteSmoothness,
                convertToProxy = _configParam.convertToProxy,
                binarySizeSelection = _configParam.binarySizeSelection,
                binaryNegative = _configParam.binaryNegative,
                binaryEncoding = _configParam.binaryEncoding,
            };
            _setup.parameters.Add(param);
            AssetDatabase.SaveAssets();
        }
    }

    public void DrawParameterConfiguration(OSCmoothParameter parameter)
    {
        EditorGUI.indentLevel = 2;
        EditorGUIUtility.labelWidth = 220f;

        var localSmoothness = parameter.localSmoothness;
        var remoteSmoothness = parameter.remoteSmoothness;
        var convertToProxy = parameter.convertToProxy;
        var binarySizeSelection = parameter.binarySizeSelection;
        var binaryNegative = parameter.binaryNegative;
        var binaryEncoding = parameter.binaryEncoding;
        var layerMask = parameter.layerMask;

        EditorGUI.BeginChangeCheck();

        localSmoothness = EditorGUILayout.FloatField(
            new GUIContent("Local Smoothness", 
                           "How much % smoothness you (locally) will see when a parameter changes value. " +
                           "Higher values represent more smoothness, and vice versa."), 
            localSmoothness);

        remoteSmoothness = EditorGUILayout.FloatField(
            new GUIContent("Remote Smoothness", 
                           "How much % smoothness remote users will see when a parameter changes value. " +
                           "Higher values represent more smoothness, and vice versa."), 
            remoteSmoothness);

        if (_advancedSettings)
            convertToProxy = EditorGUILayout.Toggle(
                new GUIContent("Proxy Conversion", 
                               "Automatically convert existing animations to use the Proxy (output) float."), 
                convertToProxy);

        layerMask = (AnimLayerTypeMask)EditorGUILayout.EnumFlagsField(
            new GUIContent("Playable Layers", 
                           "What playable layers should this parameter have OSCmooth be applied."), 
            layerMask);

        if (_setup.useBinaryDecoding)
        {
            binarySizeSelection = EditorGUILayout.Popup(
                new GUIContent("Binary Resolution", 
                               "How many steps a Binary Parameter can make. Higher values are more accurate, " +
                               "while lower values are more economic for parameter space. Recommended to use a " +
                               "Resolution of 16 or less for more space savings."), 
                binarySizeSelection, 
                binarySizeOptions);

            if (_setup.useBinaryEncoding && binarySizeSelection > 0)
                binaryEncoding = (OSCmoothParserType)EditorGUILayout.EnumPopup(
                    new GUIContent("Binary Encoder",
                                 "Method of encoding a locally driven float parameter to a synced Binary parameter" +
                                 "\n\nNone: Does not generate a local float or encoding system. Directly drive the Binary parameters from OSC." +
                                 "\n\nEncoder: Generates a float that locally drives parameters and an encoder system it drive it to synced Binary parameters. Drive the float parameter only." +
                                 "\n\nMulti Driver: Generates a local float that locally drives parameters. Directly drive the float and associated Binary parameters from OSC."),
                    binaryEncoding);

            if (binarySizeSelection > 0)
                binaryNegative = EditorGUILayout.Toggle(
                    new GUIContent("Binary Negative (+1 Bit)", 
                                   "Can this parameter output negative values? This option will add an extra bool to " +
                                   "keep track of the negative values of the parameter."), 
                    binaryNegative);
        }

        if (EditorGUI.EndChangeCheck())
        {
            parameter.localSmoothness = localSmoothness;
            parameter.remoteSmoothness = remoteSmoothness;
            if (_advancedSettings)
                parameter.convertToProxy = convertToProxy;
            else parameter.convertToProxy = true;
            parameter.layerMask = layerMask;
            parameter.binarySizeSelection = (_setup.useBinaryDecoding ? binarySizeSelection : 0);
            parameter.binaryNegative = _setup.useBinaryDecoding && binaryNegative;
            parameter.binaryEncoding = _setup.useBinaryEncoding ? binaryEncoding : OSCmoothParserType.None;
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        EditorGUI.indentLevel = 0;
    }

    private void DrawParameterUsageSection()
    {
        _oscmUsage = _setup.parameters.ParameterCost();
        int _availableParameters = _avatarDescriptor.expressionParameters.CalcAvailableSpace();
        EditorGUILayout.LabelField($"Parameter Usage: {_oscmUsage} / {_availableParameters} bits.");
    }
}