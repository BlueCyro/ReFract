using NeosModLoader;
using FrooxEngine;
using BaseX;
using UnityEngine;
using Camera = FrooxEngine.Camera;
using Component = FrooxEngine.Component;
using System.Reflection;
using UnityEngine.Rendering.PostProcessing;
using UnityNeos;

namespace ReFract;

public static class CameraHelperFunctions
{
    // Simply value transformer that converts some neos types to unity
    public static object ValueTransformer(object value)
    {
        switch(value)
        {
            case color col:
                return col.ToUnity();
            case float2 vec2:
                return vec2.ToUnity();
            case float3 vec3:
                return vec3.ToUnity();
            case float4 vec4:
                return vec4.ToUnity();
            default:
                return value;
        }
    }
    // This function will set a post processing variable on a camera that exists in a dynamic variable space
    public static void SetCameraVariable(DynamicVariableSpace? space, string CameraName, string ComponentName, string ParamName, object Value, Camera? altCameraInstance = null)
    {
        if (space == null || Engine.Current.WorldManager.FocusedWorld != space.World)
            return;
        
        bool TypeFound = ReFract.TypeLookups.TryGetValue(ComponentName, out Type CompType);
        if (!TypeFound) return;

        Camera? cam = null;
        UnityEngine.Camera? UnityCam = null;
        
        // Check if an alternative camera instance was provided to see if we can skip searching the space for one
        if (altCameraInstance != null)
            cam = altCameraInstance;
        else
            space?.TryReadValue<Camera>(ReFract.DynVarCamKeyString + CameraName, out cam);
        
        if (cam == null || (cam.Slot.Connector is SlotConnector slotConnector && !slotConnector.GeneratedGameObject.activeSelf))
            return;

        UnityCam = cam.ToUnity(); // ToUnity() wants a ton of unity references, if that's why you're questioning the excessive unity libraries >.>
        Value = ValueTransformer(Value);
        NeosMod.Debug("Re:Fract : " + CameraName + " Camera Found");

        // If the camera is not null, but the unity cam is assume it's still initializing and try running the set variable function again in a bit
        if (cam != null && UnityCam == null) // My null checks are paranoid, sue me
        {
            NeosMod.Debug($"Re:Fract : {CameraName} Camera Connector Found on {cam?.Slot.Name} but no UnityCamera, looping again");
            Engine.Current.WorldManager.FocusedWorld.RunInSeconds(0.25f, () => SetCameraVariable(space, CameraName, ComponentName, ParamName, Value));
            return;
        }
        
        if (UnityCam != null && Value != null)
        {
            var layer = UnityCam.GetComponent<PostProcessLayer>();
            if (layer == null) return;

            // Check if the component name is a post processing setting or just a generic post processing component
            bool IsPostProcessSetting = CompType.InheritsFrom(typeof(PostProcessEffectSettings));   

            // Set targe to either be a post process bundle or a component based on the above condition 
            //NeosMod.Msg("Re:Fract : " + ComponentName + " Component Found, type: " + ReFract.TypeLookups[ComponentName]);
            object? target = null;
            if (IsPostProcessSetting)
            {
                target = layer.haveBundlesBeenInited ? layer.GetBundle(CompType).settings : null;
            }
            else
                target = UnityCam.GetComponent(CompType);

            if (target == null) return;
            
            NeosMod.Debug($"Re:Fract : Camera is {cam}");
            NeosMod.Debug($"Re:Fract : setting the {ParamName} parameter of the {target.GetType()} on camera {cam?.Slot.Name} to {Value} (of type {Value.GetType()}");
            // Call into introspection and give it our override for parameters. Introspection again lets us set values on private fields without reflection
            // Also checking to see if the field name ends with an exclamaition mark, it's an easy way to tell if you wanna set a property or a field
            if (ParamName.EndsWith("!"))
            {
                Introspection.GetPropSetter(target.GetType(), ParamName.Substring(0, ParamName.Length - 1))?.Invoke(target, Value);
            }
            else
            {
                Introspection.GetFieldSetter(target.GetType(), ParamName, ReFract.ILOverride)?.Invoke(ref target, Value);
            }
        }
    }

    // Refreshes all of the post processing settings on a camera
    public static void RefreshCameraState(DynamicReferenceVariable<Camera> camVar, Camera? altCameraInstance = null)
    {
        string Name = camVar.VariableName;

        string[] splitName = Name.Split('_');

        // I'm tired, boss
        // Had to get a little reflection because I'm not a *total* masochist, and it's okay because the camera variable likely won't be updating all the time
        var handlerField = camVar.GetType().BaseType.GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);
        if (handlerField == null)
            return;
        
        var handler = handlerField.GetValue(camVar) as DynamicVariableHandler<Camera>;
        if (handler == null || handler.CurrentSpace == null) return;

        // Find the dictionary of all values on the dynamic variable space
        var spaceDictField = typeof(DynamicVariableSpace).GetField("_dynamicValues", BindingFlags.Instance | BindingFlags.NonPublic);
        if (spaceDictField == null) return;

        // Get the value of the dictionary from the current space the variable resides in
        var spaceDict = spaceDictField.GetValue(handler.CurrentSpace) as System.Collections.IDictionary;
        if (spaceDict == null)
            return;
        
        // Slow :(
        // Iterate over all of the values in the dictionary and re-apply them to the camera
        foreach (var key in spaceDict.Keys)
        {
            // Get the variable name
            string? keyName = key.GetType().GetField("name")?.GetValue(key) as string;
            string[]? stringTokens = keyName?.Split('_');
            // Make sure the variables follow the naming conventions for Re:Fract
            if (splitName.Length == 3 && stringTokens != null && stringTokens.Length == 4 && stringTokens[1] == splitName[2])
            {
                NeosMod.Debug("Re:Fract : " + keyName + " interacts with this camera");
                object? val = spaceDict[key].GetType().GetProperty("Value")?.GetValue(spaceDict[key]);
                NeosMod.Debug("Re:Fract : " + keyName + " is " + val + " from " + handler);
                if (val != null && handler != null)
                {
                    // If all is well, set the camera's post processing variables back to the ones provided by the space
                    SetCameraVariable(handler.CurrentSpace, stringTokens[1], stringTokens[2], stringTokens[3], val, altCameraInstance);
                    NeosMod.Debug("Re:Fract : Tokens are " + stringTokens[1] + " " + stringTokens[2] + " " + stringTokens[3]);
                }
            }
        }
    }

    // Function to check if an event has a specific delegate subscribed to it
    public static bool HasSubscriber(this Delegate d, Delegate c)
    {
        bool hasMethod = false;
        foreach (var m in d.GetInvocationList())
        {
            if (m.Method == c.Method)
            {
                hasMethod = true;
                break;
            }
        }
        return hasMethod;
    }
    // Thanks stackoverflow, screw you unity for not making this a default feature
    public static UnityEngine.Texture2D ResizeReal(this UnityEngine.Texture2D source, int newWidth, int newHeight)
    {
        source.filterMode = FilterMode.Point;
        UnityEngine.RenderTexture rt = UnityEngine.RenderTexture.GetTemporary(newWidth, newHeight);
        rt.filterMode = FilterMode.Point;
        UnityEngine.RenderTexture.active = rt;
        Graphics.Blit(source, rt);
        UnityEngine.Texture2D nTex = new UnityEngine.Texture2D(newWidth, newHeight, source.format, false);
        nTex.ReadPixels(new UnityEngine.Rect(0, 0, newWidth, newHeight), 0,0);
        nTex.Apply();
        UnityEngine.RenderTexture.active = null;
        UnityEngine.RenderTexture.ReleaseTemporary(rt);
        return nTex;
    }
}