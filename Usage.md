# Proper usage of Re:Fract

## <b>For a list of effects you can modify, please see: [Variables.md](Variables.md)</b>

## How do I start editing a camera?

Easy! Simply create a DynamicVariableSpace and add a DynamicReferenceVariable of type `Camera` in it somewhere, then drop in the camera you wish to modify. Since they're dynamic variables, they needn't be on the same slot.

Next, you'll need to name your camera something special so Re:Fract can pick up on it.
To mark a camera, simply change the variable name to something such as the following: `Re.Fract_Camera_MyCameraNameHere`

This will mark your camera with whatever name you choose to replace "MyCameraNameHere" with.

[Image Here]

## How do I edit a post processing effect on a camera?

The process for modifying a variable on the camera is also fairly straightforward.

To actually modify a variable, you'll want to create a DynamicValueVariable with a type that corresponds to the type of whatever parameter it is you're editing on a given post processing effect - except in the case of it being an enum, which you'll always use an int for.

The naming scheme for value variables follows this setup: `Re.Fract_MyCameraNameHere_EffectNameHere_FieldOrPropertyNameHere`

So for example, if I wanted to enable or disable the bloom, I would use: `Re.Fract_MyCameraNameHere_Bloom_enabled` as the variable name on a `DynamicValueVariable` of type `bool`.
And similarly, if I wanted to change the bloom intensity I would set up a `DynamicValueVariable` of type `float` and set its name to `Re.Fract_MyCameraNameHere_Bloom_intensity`.

You can see some examples of what this looks like in-game with the following screenshot showing a setup for the `DepthOfField` effect.

<b>Note that when you first create a variable and set its name, the effect does not automatically update on the camera, simply update the value in the variable to apply any changes.</b>

[Image here]


## Supplementary Information

Some effects (Currently just AmplifyOcclusionBase and whatever future ones that are potentially exposed) have a special property you need to set to enable or disable them.
You can reference actual properties by simply appending an exclaimation mark to the end of the field name. Example `Re.Fract_MyCameraNameHere_AmplifyOcclusionBase_enabled!`

As stated above, since these are all dynamic variables none of them need to be on the same slot as the camera, they just need to be anywhere within a dynamic variable space. They can be driven, written to, or otherwise manipulated as any normal variable can. I recommend not having duplicate value-holding variables (DynamicValueVariables or DynamicFields for example) as that will create duplicate work. Each variable you add will modify the post processing effect, so make sure you only use one per effect parameter.

Also since you can define camera names, you can manipulate multiple cameras within a single variable space by simply substituting `MyCameraNameHere` with multiple different names and camera/value variables that reference that name.
For instance, if I have variables that are called `Re.Fract_Camera_MyCamera1` and `re.Fract_Camera_MyCamera2`, you can - for example - change the bloom separately for each camera by making dynamic variables that are named `Re.Fract_MyCamera1_Bloom_enabled` and `Re.Fract_MyCamera2_Bloom_enabled` and so on.

