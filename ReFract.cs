using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using BaseX;
using CodeX;
using UnityEngine;
using Camera = FrooxEngine.Camera;
using Component = FrooxEngine.Component;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Reflection;
using UnityEngine.Rendering.PostProcessing;
using System.Reflection.Emit;
using System.Collections.Generic;
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
        // This patch looks pretty harmless, just mark our camera with a local slot to make sure we can find it later
        [HarmonyPatch(typeof(Camera), "RenderToAsset")]
        [HarmonyPrefix]
        public static void CameraMarkerPrefix(Camera __instance, int2 resolution, string format = "webp", int quality = 200)
        {
            __instance.Slot.AddLocalSlot("Re:Fract Camera Marker", false);
        }
        
        [HarmonyPatch(typeof(InteractiveCamera), "Capture", new Type[0])]
        [HarmonyPostfix]
        public static void InteractiveCamer_Capture_Postfix(InteractiveCamera __instance)
        {
            __instance.MainCamera.Target?.Slot.AddLocalSlot("Re:Fract Camera Marker", false);
        }
        // This is where things start getting shnasty
        [HarmonyPatch(typeof(RenderConnector), "RenderImmediate")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> RenderImmediateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            Msg("Re:Fract : Transpiling RenderImmediate");
            var codes = instructions.ToList();
            var localVar = il.DeclareLocal(typeof(UnityEngine.RenderTexture));
            
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo method && method.Name == "Render" && codes[i - 1].opcode == OpCodes.Ldsfld && codes[i - 1].operand is FieldInfo field && field.Name == "camera")
                {
                    // The method I'm calling here requires a bit of explanation.
                    // I basically have to go in and replace the render call so that it searches the world's local slots list for our camera marker.
                    // This is because the original camera is not sent along with the call, and the slot is the only way to find it.
                    // The goal here is to use the camera that called the render function instead of the shared camera because the shared one doesn't get any of my fancy post processing applied to it.
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = AccessTools.Method(typeof(RenderConnector_Patch), nameof(RenderConnector_Patch.RenderImmediatePrefix));
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Stloc, localVar));
                    // ^^ Storing a reference to the render texture for later
                    // I should also explain that the render texture that comes out of here is either our own camera's, or the shared camera
                    // Now you may be thinking: "Cyro, you just said you can't use the shared camera, right?"
                    // You're right. This is because if I just replace the RT in our own camera with the shared RT, it will break
                    // That is, ONLY if the camera's original RT was bigger than the shared RT that I would have otherwise shoved into it.
                    // If the shared RT is of the same size of the camera or bigger, it just magically works and has no problems. I'll explain more down below.
                    break;
                }
            }

            // Next, I have to go find ReadPixels and replace it with a proxy function. 
            // I make it so that it takes in the same arguments meaning I don't have to do anything funky on the stack
            int ReadFrom = 0;
            Type[] signature = new Type[] { typeof(UnityEngine.Rect), typeof(int), typeof(int), typeof(bool) };
            MethodInfo targetMethod = typeof(UnityEngine.Texture2D)
                .GetMethod("ReadPixels", BindingFlags.Public | BindingFlags.Instance, null, signature, null);

            var texVar = il.DeclareLocal(typeof(UnityEngine.Texture2D));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo method && method == targetMethod)
                {
                    // Here, I'm replacing ReadPixels with my own proxy function.
                    // All this proxy function does is check if the texture is bigger or smaller than the resolution of the camera.
                    // Due to what I explained above, I have to check this or else the texture breaks.
                    // Since we either pass out our own camera's texture if the shared one is smaller, or the shared one if it's bigger, we need to handle this further down in the code
                    // So this function just resizes the texture down to the queried size after rendering at the camera's original resolution - if it's smaller.
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = AccessTools.Method(typeof(RenderConnector_Patch), nameof(RenderConnector_Patch.ReadPixelsProxy));
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Stloc, texVar));
                    ReadFrom = i;
                    break;
                }
            }

            if (ReadFrom == 0)
            {
                Msg("Re:Fract : RenderImmediateTranspiler: Could not find ReadPixels method");
                return codes;
            }

            // This is a little confusing because this'll actually get called BEFORE the ReadPixels (the opcode replacement just above this comment) to set the active RT to the
            // one spat out by the RenderImmediatePrefix
            for (int i = ReadFrom; i > 0; i--)
            {
                if (codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo method && method.Name == "set_active")
                {
                    codes[i - 1].opcode = OpCodes.Ldloc;
                    codes[i - 1].operand = localVar;
                    break;
                }
            }

            // This will replace a call to the texture that's in there with a call to our own texture (being the original one *or* the shared one)
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo method && method.Name == "GetRawTextureData")
                {
                    codes[i - 1].opcode = OpCodes.Ldloc;
                    codes[i - 1].operand = texVar;
                    break;
                }
            }
            Msg("Re:Fract : Transpiling RenderImmediate done");
            return codes.ToArray();
        }

        // I've already explained these above, but I'll sprinkle in some sparse comments so you can understand the horror a bit better
        public static UnityEngine.RenderTexture RenderImmediatePrefix(UnityEngine.Camera originalCamera)
        {
            // Shorthand delegate for finding and destroying the temporary marker
            Action<Slot> DestroyMarker = new Action<Slot>(slot =>
            {
                slot.World.RunSynchronously(() => {
                    slot.Parent.Tag = "Re:Fract Camera Marked Object";
                    slot.Destroy();
                });
            });

            // Find the local slots so we can iterate through it and find our camera
            World curWorld = Engine.Current.WorldManager.FocusedWorld;
            var worldLocalSlotsField = typeof(World).GetField("_localSlots", BindingFlags.Instance | BindingFlags.NonPublic);
            
            List<Slot> localSlots = (List<Slot>)worldLocalSlotsField.GetValue(curWorld);

            // Since First() throws if it finds nothing, we try/catch it
            Slot? marker = null;
            try
            {
                marker = localSlots.First(s => s.Name == "Re:Fract Camera Marker");
            }
            catch (InvalidOperationException)
            {
                // If we fail (meaning there is no marker) just render normally and return the original texture
                originalCamera.Render();
                return originalCamera.targetTexture;
            }

            Camera? neosCam = marker.Parent.GetComponent<Camera>();
            UnityEngine.Camera? camC = (neosCam?.Connector as CameraConnector)?.UnityCamera;
            if (neosCam == null || camC == null)
            {
                // This is a bit paranoid, but if either of these are null, just render normally and return the original texture
                originalCamera.Render();
                DestroyMarker(marker);
                return originalCamera.targetTexture;
            }

            // Like I said, here we check the size of the shared camera's RT and our own camera's RT.
            // If the shared one is bigger (meaning replacing the one in our camera will work magically for some reason), we can render onto it just fine
            Msg("Re:Fract : RenderImmediatePrefix: " + camC.targetTexture);
            bool isOrigBigger = originalCamera.targetTexture.width > camC.targetTexture.width || originalCamera.targetTexture.height > camC.targetTexture.height;
            if (isOrigBigger)
            {
                var backupTex = camC.targetTexture;
                camC.targetTexture = originalCamera.targetTexture;
                camC.Render();
                camC.targetTexture = backupTex;
            }
            else
            {
                // Otherwise, we render onto our own camera's RT and then return that
                camC.Render();
            }

            DestroyMarker(marker);
            
            var tex = isOrigBigger ? originalCamera.targetTexture : camC.targetTexture;
            return tex;
        }

        // Thanks stackoverflow, screw you unity for not making this a default feature
        public static UnityEngine.Texture2D ResizeReal(UnityEngine.Texture2D source, int newWidth, int newHeight)
        {
            source.filterMode = FilterMode.Point;
            UnityEngine.RenderTexture rt = UnityEngine.RenderTexture.GetTemporary(newWidth, newHeight);
            rt.filterMode = FilterMode.Point;
            UnityEngine.RenderTexture.active = rt;
            Graphics.Blit(source, rt);
            UnityEngine.Texture2D nTex = new UnityEngine.Texture2D(newWidth, newHeight);
            nTex.ReadPixels(new UnityEngine.Rect(0, 0, newWidth, newHeight), 0,0);
            nTex.Apply();
            UnityEngine.RenderTexture.active = null;
            UnityEngine.RenderTexture.ReleaseTemporary(rt);
            return nTex;
        }

        public static UnityEngine.Texture2D ReadPixelsProxy(UnityEngine.Texture2D tex, UnityEngine.Rect rect, int destX, int destY, bool recalcMips)
        {
            // This is foul.
            // If the queried size is smaller than the original texture, we need to resize the texture to the queried size
            var activeTex = UnityEngine.RenderTexture.active;
            int x = activeTex != null ? activeTex.width : (int)rect.width;
            int y = activeTex != null ? activeTex.height : (int)rect.height;


            bool isBigger = rect.width < x || rect.height < y;
            if (isBigger)
            {
                var rectBig = new UnityEngine.Rect(0, 0, x, y);
                UnityEngine.Texture2D newText = new UnityEngine.Texture2D(x, y, tex.format, false);
                newText.ReadPixels(rectBig, destX, destY, recalcMips);
                newText.Apply();
                // It is absolutely shameful that unity doesn't have an easy function for resizing textures.
                // Oh wait, it does but it doesn't actually rescale the texture, just the dimensions of it!!!!
                var resized = ResizeReal(newText, tex.width, tex.height);
                tex.SetPixels(resized.GetPixels());
                return tex;
            }
            else
            {
                // If the queried size is the same or bigger than the original texture, we can just read the pixels normally >.>
                tex.ReadPixels(rect, destX, destY, recalcMips);
                return tex;
            }
        }
    }
}
