using NeosModLoader;
using System.Reflection;
using System.Reflection.Emit;
namespace ReFract;

// The things I do to avoid reflection...
// I'm sorry. - Github Copilot
public static class Introspection
{
    public delegate void RefAction<T1, T2>(ref T1 obj, T2 value);
    // Dictionaries to hold the type delegates, makes for easy lookups
    public static Dictionary<Type, Dictionary<string, RefAction<object, object>>> _cachedSetters = new Dictionary<Type, Dictionary<string, RefAction<object, object>>>();
    public static Dictionary<Type, Dictionary<string, Action<object, object>>> _cachedPropSetters = new Dictionary<Type, Dictionary<string, Action<object, object>>>();
    public static RefAction<object, object>? GetFieldSetter(Type obj, string fieldName, Func<Type, FieldInfo, ILGenerator, bool>? ilOverride = null)
    {
        // Try to return an existing delegate, otherwise create one
        try
        {
            return _cachedSetters[obj][fieldName];
        }
        catch
        {
            if (obj == null || fieldName == null || fieldName.Length == 0)
                return null;
            NeosMod.Debug("Introspection : Getting field");
            // Get the target field
            FieldInfo field = obj.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            NeosMod.Debug("Introspection : Field is " + (field == null ? "null" : "not null"));
            if (field == null)
                return null;
            
            NeosMod.Debug("Introspection : Field is " + field.Name);
            // Get the delegate that acts as a field accessor the target field
            var del = GetDynamicMethod(obj, field, ilOverride);
            NeosMod.Debug("Introspection : Delegate is " + (del == null ? "null" : "not null & " + del.GetType().ToString()));
            
            if (del == null)
                return null;
            
            // Add the delegate to the dictionary
            if (!_cachedSetters.ContainsKey(obj))
                _cachedSetters.Add(obj, new Dictionary<string, RefAction<object, object>>());
            
            _cachedSetters[obj].Add(fieldName, del);
            NeosMod.Debug("Introspection : Added delegate to dictionary at " + obj.ToString() + "." + fieldName);
            return del;
        }
    }

    // Now for properties

    public static Action<object, object>? GetPropSetter(Type obj, string propName)
    {
        // Try to return an existing delegate, otherwise create one
        try
        {
            return _cachedPropSetters[obj][propName];
        }
        catch
        {
            if (obj == null || propName == null || propName.Length == 0)
                return null;
            
            NeosMod.Debug("Introspection : Getting property");
            // Get the target property
            PropertyInfo prop = obj.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            NeosMod.Debug("Introspection : Property is " + (prop == null ? "null" : "not null"));
            if (prop == null)
                return null;
            
            NeosMod.Debug("Introspection : Property is " + prop.Name);
            // Get the delegate that acts as a property accessor the target property
            var del = GetDynamicPropMethod(obj, prop);
            NeosMod.Debug("Introspection : Delegate is " + (del == null ? "null" : "not null & " + del.GetType().ToString()));
            
            if (del == null)
                return null;
            
            // Add the delegate to the dictionary
            if (!_cachedPropSetters.ContainsKey(obj))
                _cachedPropSetters.Add(obj, new Dictionary<string, Action<object, object>>());
            
            _cachedPropSetters[obj].Add(propName, del);
            NeosMod.Debug("Introspection : Added delegate to dictionary at " + obj.ToString() + "." + propName);
            return del;
        }
    }

    // This function takes in a type, field, and an override for if you want to input your own custom IL setter to handle the field
    // Returns a delegate that acts as a field setter for the target field
    public static RefAction<object, object> GetDynamicMethod(Type obj, FieldInfo field, Func<Type, FieldInfo, ILGenerator, bool>? ilOverride = null)
    {
        // Create a dynamic method that takes in the target object by reference and accesses a field on it
        var method = new DynamicMethod("", null, new Type[] { typeof(object).MakeByRefType(), typeof(object) }, true);
        var il = method.GetILGenerator(256);
        Label typeFailed = il.DefineLabel();
        
        // The ilOverride will return a bool for if it was successfully able to generate IL or not. If not, it just generates a default accessor
        if (ilOverride == null || !ilOverride(obj, field, il))
        {
            // Load the value onto the stack, grab the type, and check if it matches the type that the delegate is supposed to handle. If not, jump to the failure state
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, typeof(object).GetMethod("GetType"));
            il.Emit(OpCodes.Ldtoken, field.FieldType);
            il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
            il.Emit(OpCodes.Ceq);
            il.Emit(OpCodes.Brfalse, typeFailed);

            // Otherwise, load the target object in by reference, cast it to the correct handling type, load the value, unbox it as the proper type, and store it in the target field.
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Castclass, obj);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Unbox_Any, field.FieldType);
            il.Emit(OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);

            // If the type doesn't match, print out a message and return, doing nothing.
            il.MarkLabel(typeFailed);
            il.Emit(OpCodes.Ldstr, $"Re:Fract : Wrong type for field \"{field.Name}\" which takes \"{field.FieldType}\"");
            il.Emit(OpCodes.Call, typeof(NeosMod).GetMethod("Msg", new Type[] { typeof(string) }));
            il.Emit(OpCodes.Ret);
            NeosMod.Debug("Introspection : Generated dynamic method with default IL");
        }
        NeosMod.Debug("Introspection : Creation of DynamicMethod was successful for " + obj.ToString() + "." + field.Name);
        return (RefAction<object, object>)method.CreateDelegate(typeof(RefAction<,>).MakeGenericType(typeof(object), typeof(object)));
    }

    // Dynamic method getter for properties
    public static Action<object, object>? GetDynamicPropMethod(Type obj, PropertyInfo prop)
    {
        if (obj == null || prop == null || !prop.CanWrite)
            return null;
        
        // Create a delegate from the set method
        var method = prop.GetSetMethod(true);
        if (method == null)
            return null;
        
        var del = new DynamicMethod("", null, new Type[] { typeof(object), typeof(object) }, true);
        var il = del.GetILGenerator(256);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, obj);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Unbox_Any, prop.PropertyType);
        il.Emit(OpCodes.Call, method);
        il.Emit(OpCodes.Ret);

        NeosMod.Debug("Introspection : Created delegate for property " + prop.Name);

        var ret = (Action<object, object>)del.CreateDelegate(typeof(Action<object, object>));
        // Add the delegate to the dictionary
        
        return ret;
    }
}