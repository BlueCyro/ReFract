using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using BaseX;
using UnityEngine;
using Camera = FrooxEngine.Camera;
using Component = FrooxEngine.Component;
using System.Text;
using System.Reflection;
using UnityEngine.Rendering.PostProcessing;
using System.Reflection.Emit;
using UnityNeos;

namespace ReFract;
public class ReFract : NeosMod
{
    public override string Author => "Cyro";
    public override string Name => "ReFract";
    public override string Version => "1.0.0";
    public static string DynVarKeyString => "Re.Fract_";
    public static string DynVarCamKeyString => "Re.Fract_Camera_";
    public static string ReFractTag => "Re:FractCameraSpace";
    public static Dictionary<string, Type> TypeLookups = new Dictionary<string, Type>();
    public static Type[] SupportedTypes = new Type[] { typeof(bool), typeof(int), typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Color) };

    // This is an override for Introspection - which generates accessor delegates for fields because reflection is slow and I hate it.
    // Specifically so it can handle Unity's ParameterOverride<T> types.
    public static bool ILOverride(Type obj, FieldInfo field, ILGenerator il)
    {   
        Type BaseFieldType = field.FieldType.BaseType;
        Label typeFailed = il.DefineLabel();
        Label acceptIntAsEnum = il.DefineLabel();
        Label cont = il.DefineLabel();
        if (BaseFieldType.IsGenericType && BaseFieldType.GetGenericTypeDefinition() == typeof(ParameterOverride<>))
        {
            // If the parameter isn't an enum, just continue as normal
            il.Emit(OpCodes.Ldtoken, BaseFieldType.GetGenericArguments()[0]);
            il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
            il.Emit(OpCodes.Call, typeof(Type).GetProperty("IsEnum").GetGetMethod());
            il.Emit(OpCodes.Brfalse, cont);

            // If the parameter is an enum, but isn't an int, continue as normal
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, typeof(object).GetMethod("GetType"));
            il.Emit(OpCodes.Ldtoken, typeof(int));
            il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
            il.Emit(OpCodes.Ceq);
            il.Emit(OpCodes.Brfalse, cont);
            // If both conditions are met, accept the int to be used as an enum
            il.Emit(OpCodes.Br_S, acceptIntAsEnum);

            // If argument 1 doesn't match the generic arguments of the field (indicating that it's not the correct type), jump to the failed label
            il.MarkLabel(cont);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, typeof(object).GetMethod("GetType"));
            il.Emit(OpCodes.Ldtoken, BaseFieldType.GetGenericArguments()[0]);
            il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
            il.Emit(OpCodes.Ceq);
            il.Emit(OpCodes.Brfalse, typeFailed);

            // Else, load arg0 by reference, cast it to the correct type, unbox arg1 to the correct type, create a new parameter with our arguments and store it into the field
            il.MarkLabel(acceptIntAsEnum);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Castclass, obj);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Unbox_Any, BaseFieldType.GetGenericArguments()[0]);
            il.Emit(OpCodes.Newobj, BaseFieldType.GetConstructor(new Type[] { BaseFieldType.GetGenericArguments()[0] }));
            il.Emit(OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);

            // Call Msg() and print out a silly message if we failed to cast the argument
            il.MarkLabel(typeFailed);
            il.Emit(OpCodes.Ldstr, $"Re:Fract : Wrong type for field \"{field.Name}\" which takes \"{BaseFieldType.GetGenericArguments()[0]}\"! You fool!");
            il.Emit(OpCodes.Call, typeof(NeosMod).GetMethod("Msg", new Type[] { typeof(string) }));
            il.Emit(OpCodes.Ret);
            return true;
        }
        return false;
    }
    
    public override void OnEngineInit()
    {
        // Register some events so that when we switch from world to world, the cameras will get refreshed since the post processing likes to reset whenever the gameobjects are disabled
        Engine.Current.RunPostInit(() => {
            Action<World> worldAction = new Action<World> (w => {
                w.RootSlot.GetComponentsInChildren<DynamicReferenceVariable<Camera>>((var) => {
                    return var.Reference.Target != null && var.VariableName.Value.StartsWith(DynVarCamKeyString);
                }).ForEach(cam => {
                    cam.World.RunInUpdates(2, () => CameraHelperFunctions.RefreshCameraState(cam, cam.Reference.Target));
                });
            });
            Engine.Current.WorldManager.WorldFocused += worldAction;
            Engine.Current.WorldManager.WorldAdded += worldAction;
        });
        
        // Msg($"Re:Fract : Unity version: {UnityEngine.Application.version} ({UnityEngine.Application.unityVersion})");
        Harmony harmony = new Harmony("net.Cyro.ReFract");

        // Get all types that inherit from PostProcessEffectSettings
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsSubclassOf(typeof(PostProcessEffectSettings)))
                {
                    TypeLookups.Add(type.Name, type);
                }
            }
        }
        TypeLookups.Add("AmplifyOcclusionBase", typeof(AmplifyOcclusionBase)); // Include this specifically since it does post processing, but is not part of the bundle stack
        // TypeLookups will be used to easily get a type from one specified in a dynamic variable name string

        harmony.PatchAll();

        // Optional code for generating a supported types list

        // StringBuilder sb = new StringBuilder();
        // Func<Type, Type> GetTrueFieldType = (type) => {
        //     if (type.BaseType.IsGenericType && type.BaseType.GetGenericTypeDefinition() == typeof(ParameterOverride<>))
        //     {
        //         return type.BaseType.GetGenericArguments()[0];
        //     }
        //     Msg($"Re:Fract : Type \"{type.Name}\" ({type.BaseType}) is not a ParameterOverride type!");
        //     return type;
        // };

        // foreach (Type t in TypeLookups.Values)
        // {
        //     sb.AppendLine($"## {t.Name}:");
        //     FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public);
        //     PropertyInfo[] props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        //     sb.AppendLine("### Fields");
        //     foreach (PropertyInfo prop in props)
        //     {
        //         if (prop.CanWrite && prop.CanRead && prop.Name == "enabled")
        //         {
        //             sb.AppendLine($"- {prop.Name}! ({prop.PropertyType.Name})");
        //         }
        //     }
        //     foreach (FieldInfo field in fields)
        //     {
        //         if ((SupportedTypes.Contains(GetTrueFieldType(field.FieldType)) || GetTrueFieldType(field.FieldType).IsEnum) && field.Name != "active")
        //         {
        //             sb.Append($"- {field.Name} ({(GetTrueFieldType(field.FieldType).IsEnum ? "Int32" : GetTrueFieldType(field.FieldType).Name)})");
        //             if (GetTrueFieldType(field.FieldType).IsEnum)
        //             {
        //                 sb.Append($" - Enum values: ");
        //                 foreach (object val in Enum.GetValues(GetTrueFieldType(field.FieldType)))
        //                 {
        //                     sb.Append($"{val.ToString()} ({(int)val}), ");
        //                 }
        //             }
        //             sb.AppendLine();
        //         }
        //     }
        //     sb.AppendLine();
        // }

        // File.WriteAllText("ReFract_TypeLookups.txt", sb.ToString());
    }
    [HarmonyPatch]
    public class DynamicVariableBase_Patch
    {
        // Patching ComponentBase because harmony A) Sucks at patching generic methods, and B) The entry component I had to target doesn't override OnStart
        [HarmonyPatch(typeof(ComponentBase<Component>), "OnStart")]
        [HarmonyPostfix]
        public static void ParentReferencePostfix(ComponentBase<Component> __instance)
        {
            // Check if the component is a dynamic value variable
            Type t = __instance.GetType();
            if (t.IsConstructedGenericType && t.GetGenericTypeDefinition() == typeof(DynamicValueVariable<>))
            {
                // Get the field reference for the "Value" field on the variable
                FieldInfo? field = t.GetField("Value", BindingFlags.Instance | BindingFlags.Public);
                if (field == null)
                    return;
                
                // Get the IField for it
                IField Value = (IField)field.GetValue(__instance);
                
                // Get the handler field for the dynamic variable (the meat of dynvars)
                var handlerField = t.BaseType.GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);
                
                // This is a bit unorthodox, but it creates a delegate that lets me access the handler field from a dynvar without reflection
                // The reason I make a delegate is because you could be changing many of these variables many times a second, and I don't want reflection slowing it down considerably
                // Also efficiently calls DynvarCamSetter<T> for whatever type the variable is.
                var method = new DynamicMethod("", null, new Type[] { typeof(object) }, true);
                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, t);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldfld, handlerField);
                il.Emit(OpCodes.Call, typeof(DynamicVariableBase_Patch).GetMethod("DynvarCamSetter").MakeGenericMethod(t.GetGenericArguments()[0]));
                il.Emit(OpCodes.Ret);
                
                var methodDelegate = (Action<object>)method.CreateDelegate(typeof(Action<object>));
                
                // Subscribe to the Changed event on the value field.
                // I sorely wish I could have just patched the function in the variable space that handles all changes variables send ;_;
                Value.Changed += (IChangeable c) => {
                    methodDelegate(__instance);
                };
            }
            // Specifically also check if it's a camera variable
            else if (t == typeof(DynamicReferenceVariable<Camera>))
            {
                // Get the Reference fieldinfo
                FieldInfo? field = t.GetField("Reference", BindingFlags.Instance | BindingFlags.Public);
                if (field == null) return;

                // Get the actual syncref
                ISyncRef Reference = (ISyncRef)field.GetValue(__instance);
                DynamicReferenceVariable<Camera>? camVar = __instance as DynamicReferenceVariable<Camera>;
                // Store the name of the variable for later
                string? CamName = camVar?.VariableName.Value;

                // I *should* be okay not unsubbing these events since the object they're attached to gets destroyed (and thus the object listening to these events)
                Reference.Changed += (IChangeable c) => {
                    string? Name = camVar?.VariableName.Value;
                    SyncRef<Camera>? refVar = c as SyncRef<Camera>;

                    // Handler delegate that can unsubscribe itself
                    // This'll be used for when a camera is dropped into the dynvar.
                    // Will be used to subscribe to changed events on the camera's "PostProcessing" checkbox and the camera's slot's "Active" checkbox, and refreshes the camera whenever they become active.
                    Action<IChangeable>? handler = null;
                    handler = delegate(IChangeable c2)
                    {
                        if (c2 is Sync<bool> active && refVar != null && refVar.Target != null && camVar != null)
                        {
                            Msg($"Re:Fract : Camera \"{Name}\" is {(active ? "active" : "inactive")}!");
                            if (active)
                            {
                                Engine.Current.WorldManager.FocusedWorld.RunInUpdates(2, () => CameraHelperFunctions.RefreshCameraState(camVar, refVar.Target as Camera));
                            }
                        }
                        else if (c2 is Sync<bool> active2)
                        {
                            Msg($"Re:Fract : Camera \"{Name}\" is no longer valid, unsubscribing!");
                            active2.Changed -= handler;
                        }
                    };
                    // If the camera is dropped into the dynvar, refresh it, then subscribe to the aforementioned changed events.
                    if (camVar != null && Name != null && Name.StartsWith(DynVarCamKeyString) && refVar != null && refVar.Target != null)
                    {
                        CameraHelperFunctions.RefreshCameraState(camVar, refVar.Target as Camera);
                        Msg($"Re:Fract : Camera \"{Name}\" updated!");
                        
                        // Just to be extra safe ;) (Though I am under no illusions that juggling events is uh... not great)
                        Sync<bool> cameraSlotActive = refVar.Target.Slot.ActiveSelf_Field;
                        Sync<bool> cameraPostProcessActive = refVar.Target.Postprocessing;

                        var ev = typeof(SyncElement).GetEvent("Changed", BindingFlags.Public | BindingFlags.Instance);
                        var fi = typeof(SyncElement).GetField(ev.Name, AccessTools.all);

                        // Make sure we aren't already subscribed so we don't add duplicate subscriptions.
                        Delegate? del = fi.GetValue(refVar.Target.Slot.ActiveSelf_Field) as Delegate;
                        if (del == null || !del.HasSubscriber(handler))
                        {
                            refVar.Target.Slot.ActiveSelf_Field.Changed += handler;
                            Msg($"Re:Fract : Camera \"{Name}\" subscribed to slot active state!");
                        }
                        
                        del = fi.GetValue(refVar.Target.Postprocessing) as Delegate;
                        if (del == null || !del.HasSubscriber(handler))
                        {
                            refVar.Target.Postprocessing.Changed += handler;
                            Msg($"Re:Fract : Camera \"{Name}\" subscribed to postprocessing state!");
                        }
                    }
                };
                // Whenever an existing variable starts, refresh the camera state. The camera state has to be update like all the time, otherwise it'll be reset
                if (camVar != null && CamName != null && CamName.StartsWith(DynVarCamKeyString))
                {
                    string[] splitName = CamName.Split('_');
                    if (splitName.Length == 3)
                    {
                        // Wait for any variable spaces and such to initialize
                        __instance.World.RunInUpdates(3, () => {
                            Msg("Re:Fract : Starting " + splitName[2]);
                            CameraHelperFunctions.RefreshCameraState(camVar, camVar.Reference.Target);
                            camVar.Reference.SyncElementChanged(camVar.Reference);
                        });
                    }
                }
            }
        }

        // Function that gets called whenever a dynamic value changes so it can set a post processing option on the camera
        public static void DynvarCamSetter<T>(DynamicVariableBase<T> __instance, DynamicVariableHandler<T> handler)
        {
            string Name = __instance.VariableName;

            //Msg("Re:Fract: DynamicVariableBase_Patch: " + Name + ": " + typeof(T).Name);
            if (Name != null && Name.StartsWith(DynVarKeyString))
            {
                T Value = __instance.DynamicValue;
                if (Value == null)
                    return;
                
                DynamicVariableSpace Space = handler.CurrentSpace;
                
                string[] camParams = Name.Split('_');
                if (camParams.Length == 4 && Name.StartsWith(DynVarKeyString))
                {
                    string camName = camParams[1];
                    string componentName = camParams[2];
                    string paramName = camParams[3];
                    CameraHelperFunctions.SetCameraVariable(Space, camName, componentName, paramName, Value);
                }
            }
        }
    }


    // This class can not be healthy for you
    // This whole mess is not something I'm proud of, but my knowledge of unity is lacking and I - for the life of me - could not make it do what I want
    // If you can make it better, tell me and I will HAPPILY fix it because I am ashamed.
    [HarmonyPatch]
    public static class RenderConnector_Patch
    {
        /*
        [HarmonyPatch(typeof(RenderConnector), "RenderImmediate")]
        [HarmonyReversePatch]
        public static byte[] RenderImmediate(FrooxEngine.RenderSettings renderSettings)
        {
            throw new NotImplementedException();
        }
        */
        // This patch looks pretty harmless, just mark our camera with a local slot to make sure we can find it later
        [HarmonyPatch(typeof(Camera), "RenderToAsset")]
        [HarmonyPrefix]
        public static void CameraMarkerPrefix(Camera __instance, int2 resolution, string format = "webp", int quality = 200)
        {
            __instance.Slot.AddLocalSlot("Re:Fract Camera Marker", false);
        }
        
        [HarmonyPatch(typeof(InteractiveCamera), "Capture", new Type[] { typeof(InteractiveCamera.Mode), typeof(int2), typeof(bool) })]
        [HarmonyPostfix]
        public static void InteractiveCamera_Capture_Postfix(InteractiveCamera __instance)
        {
            __instance.MainCamera.Target?.Slot.AddLocalSlot("Re:Fract Camera Marker", false);
        }

        [HarmonyPatch(typeof(RenderConnector), "RenderImmediate")]
        [HarmonyPrefix]
        public static bool RenderImmediatePrefix(ref byte[] __result, RenderConnector __instance, FrooxEngine.RenderSettings renderSettings)
        {
            if (renderSettings.fov >= 180f)
            {
                return true;
            }
            
            // If it's bigger then we render onto a new texture, otherwise we render onto the existing one and then downscale it

            // Find the local slots so we can iterate through it and find our camera
            World curWorld = Engine.Current.WorldManager.FocusedWorld;
            var worldLocalSlotsField = typeof(World).GetField("_localSlots", BindingFlags.Instance | BindingFlags.NonPublic);
            
            List<Slot> localSlots = (List<Slot>)worldLocalSlotsField.GetValue(curWorld);
            Slot? marker = null;
            try
            {
                marker = localSlots.First(s => s.Name == "Re:Fract Camera Marker");
            }
            catch
            {
                // If we can't find the marker, we can't do anything
                Msg("Re:Fract: RenderConnector_Patch: Could not find camera marker!");
                return true;
            }

            marker.RunSynchronously(() => {
                if (marker == null) return;
                marker.Parent.Tag = "Re:Fract Camera Marked Object";
                marker.Destroy();
            });

            Camera? cam = marker.Parent.GetComponent<Camera>();
            UnityEngine.Camera? unityCam = (cam.Connector as CameraConnector)?.UnityCamera;
            UnityEngine.RenderTexture? renderTexture = unityCam?.targetTexture;
            
            if (unityCam == null || renderTexture == null) return true;

            int2 camRes = new int2(renderTexture.width, renderTexture.height);

            int2 queriedRes = renderSettings.size;

            if ((queriedRes > camRes).Any())
            {
                Msg("Re:Fract: RenderConnector_Patch: Queried resolution is bigger than camera resolution");
                UnityEngine.Texture2D tex = new UnityEngine.Texture2D(queriedRes.x, queriedRes.y, renderSettings.textureFormat.ToUnity(true), false);
                UnityEngine.RenderTexture temp = UnityEngine.RenderTexture.GetTemporary(queriedRes.x, queriedRes.y, 24, RenderTextureFormat.ARGB32);
                UnityEngine.RenderTexture active = UnityEngine.RenderTexture.active;
                
                UnityEngine.RenderTexture old = unityCam.targetTexture;
                unityCam.targetTexture = temp;
                unityCam.Render();
                unityCam.targetTexture = old;

                UnityEngine.RenderTexture.active = temp;
                tex.ReadPixels(new UnityEngine.Rect(0, 0, queriedRes.x, queriedRes.y), 0, 0, false);
                tex.Apply();
                UnityEngine.RenderTexture.active = active;
                
                UnityEngine.RenderTexture.ReleaseTemporary(temp);
                byte[] bytes = tex.GetRawTextureData();
                UnityEngine.Object.Destroy(tex);
                __result = bytes;
                return false;
            }
            else
            {
                UnityEngine.RenderTexture active = UnityEngine.RenderTexture.active;
                UnityEngine.Texture2D tex = new UnityEngine.Texture2D(camRes.x, camRes.y, renderSettings.textureFormat.ToUnity(true), false);

                unityCam.Render();
                UnityEngine.RenderTexture.active = renderTexture;
                tex.ReadPixels(new UnityEngine.Rect(0, 0, camRes.x, camRes.y), 0, 0, false);
                tex.Apply();
                UnityEngine.RenderTexture.active = active;

                UnityEngine.Texture2D resized = tex.ResizeReal(queriedRes.x, queriedRes.y);
                byte[] bytes = resized.GetRawTextureData();
                UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(resized);
                __result = bytes;
                return false;
            }
        }
    }
}
