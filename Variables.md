## AmbientOcclusion:
### Fields
- mode (Int32) - Enum values: ScalableAmbientObscurance (0), MultiScaleVolumetricObscurance (1), 
- intensity (Single)
- color (Color)
- ambientOnly (Boolean)
- noiseFilterTolerance (Single)
- blurTolerance (Single)
- upsampleTolerance (Single)
- thicknessModifier (Single)
- directLightingStrength (Single)
- radius (Single)
- quality (Int32) - Enum values: Lowest (0), Low (1), Medium (2), High (3), Ultra (4), 
- enabled (Boolean)

## AutoExposure:
### Fields
- filtering (Vector2)
- minLuminance (Single)
- maxLuminance (Single)
- keyValue (Single)
- eyeAdaptation (Int32) - Enum values: Progressive (0), Fixed (1), 
- speedUp (Single)
- speedDown (Single)
- enabled (Boolean)

## Bloom:
### Fields
- intensity (Single)
- threshold (Single)
- softKnee (Single)
- clamp (Single)
- diffusion (Single)
- anamorphicRatio (Single)
- color (Color)
- fastMode (Boolean)
- dirtIntensity (Single)
- enabled (Boolean)

## ChromaticAberration:
### Fields
- intensity (Single)
- fastMode (Boolean)
- enabled (Boolean)

## ColorGrading:
### Fields
- gradingMode (Int32) - Enum values: LowDefinitionRange (0), HighDefinitionRange (1), External (2), 
- tonemapper (Int32) - Enum values: None (0), Neutral (1), ACES (2), Custom (3), 
- toneCurveToeStrength (Single)
- toneCurveToeLength (Single)
- toneCurveShoulderStrength (Single)
- toneCurveShoulderLength (Single)
- toneCurveShoulderAngle (Single)
- toneCurveGamma (Single)
- ldrLutContribution (Single)
- temperature (Single)
- tint (Single)
- colorFilter (Color)
- hueShift (Single)
- saturation (Single)
- brightness (Single)
- postExposure (Single)
- contrast (Single)
- mixerRedOutRedIn (Single)
- mixerRedOutGreenIn (Single)
- mixerRedOutBlueIn (Single)
- mixerGreenOutRedIn (Single)
- mixerGreenOutGreenIn (Single)
- mixerGreenOutBlueIn (Single)
- mixerBlueOutRedIn (Single)
- mixerBlueOutGreenIn (Single)
- mixerBlueOutBlueIn (Single)
- lift (Vector4)
- gamma (Vector4)
- gain (Vector4)
- enabled (Boolean)

## DepthOfField:
### Fields
- focusDistance (Single)
- aperture (Single)
- focalLength (Single)
- kernelSize (Int32) - Enum values: Small (0), Medium (1), Large (2), VeryLarge (3), 
- enabled (Boolean)

## Grain:
### Fields
- colored (Boolean)
- intensity (Single)
- size (Single)
- lumContrib (Single)
- enabled (Boolean)

## LensDistortion:
### Fields
- intensity (Single)
- intensityX (Single)
- intensityY (Single)
- centerX (Single)
- centerY (Single)
- scale (Single)
- enabled (Boolean)

## MotionBlur:
### Fields
- shutterAngle (Single)
- sampleCount (Int32)
- enabled (Boolean)

## ScreenSpaceReflections:
### Fields
- preset (Int32) - Enum values: Lower (0), Low (1), Medium (2), High (3), Higher (4), Ultra (5), Overkill (6), Custom (7), 
- maximumIterationCount (Int32)
- resolution (Int32) - Enum values: Downsampled (0), FullSize (1), Supersampled (2), 
- thickness (Single)
- maximumMarchDistance (Single)
- distanceFade (Single)
- vignette (Single)
- enabled (Boolean)

## Vignette:
### Fields
- mode (Int32) - Enum values: Classic (0), Masked (1), 
- color (Color)
- center (Vector2)
- intensity (Single)
- smoothness (Single)
- roundness (Single)
- rounded (Boolean)
- opacity (Single)
- enabled (Boolean)

## AmplifyOcclusionBase:
### Fields
- enabled! (Boolean)
- ApplyMethod (Int32) - Enum values: PostEffect (0), Deferred (1), Debug (2), 
- SampleCount (Int32) - Enum values: Low (0), Medium (1), High (2), VeryHigh (3), 
- PerPixelNormals (Int32) - Enum values: None (0), Camera (1), GBuffer (2), GBufferOctaEncoded (3), 
- Intensity (Single)
- Tint (Color)
- Radius (Single)
- PowerExponent (Single)
- Bias (Single)
- Thickness (Single)
- Downsample (Boolean)
- CacheAware (Boolean)
- FadeEnabled (Boolean)
- FadeStart (Single)
- FadeLength (Single)
- FadeToIntensity (Single)
- FadeToTint (Color)
- FadeToRadius (Single)
- FadeToPowerExponent (Single)
- FadeToThickness (Single)
- BlurEnabled (Boolean)
- BlurRadius (Int32)
- BlurPasses (Int32)
- BlurSharpness (Single)
- FilterEnabled (Boolean)
- FilterBlending (Single)
- FilterResponse (Single)

