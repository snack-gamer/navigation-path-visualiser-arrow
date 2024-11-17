# Navigation Path Visualizer

![Screenshot 2024-11-17 at 8 49 21 PM](https://github.com/user-attachments/assets/55b95d55-6d43-4beb-9222-bfc8c6bb3315)

Imagine you're creating a third-person exploration game where players can set waypoints on a map to navigate to points of interest. To enhance the player experience, you want to display a visual path guiding them from their current location to the selected destination.

A dynamic navigation path visualization system for Unity that calculates and renders optimal paths using NavMesh sampling.

## Overview

![Screenshot 2024-11-17 at 8 49 50 PM](https://github.com/user-attachments/assets/c845fbf3-2bee-4409-a0c8-82ccb735b84b)

Nav Path Visualizer helps create third-person exploration games where players can navigate to points of interest using visual path guides. The system calculates optimal paths using Unity's NavMesh and renders them in real-time.

![Path-Nav-Visualizer](https://github.com/user-attachments/assets/a6a5ca5c-14b7-4196-976c-82e59dcd6540)


## Features

```
Real-time path calculation between two transforms
Dynamic path updates based on player movement and environment changes
Customizable visual appearance (width, color, height)
Support for partial paths
Debug visualization options
Configurable update intervals
Smooth path rendering with adjustable subdivisions
```


## Usage

```python
# Get reference to the 'PathGuideSystem'
PathGuideSystem pathGuideSystem = GetComponent<PathGuideSystem>();

# Set transform targets
pathGuideSystem.SetTransforms(transformA, transformB);

# Or set individually
pathGuideSystem.TargetA = transformA;
pathGuideSystem.TargetB = transformB;

```

## Configuration Options


```python
Line Height --> Vertical offset of the path above the ground
Update Interval --> Frequency of path recalculation
Smoothing Subdivisions --> Path line smoothness control

Path Appearance:

Scroll Speed
Path Width
Path Color
Arrow Tiles

NavMesh Settings:

Sample Distance
Debug Visualization
Partial Path Allow
```

## Notes

Path updates occur at specified intervals
Debug information can be toggled in the Inspector
System automatically handles target transform movement

## License

[MIT](https://choosealicense.com/licenses/mit/)
