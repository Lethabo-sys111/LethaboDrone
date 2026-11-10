# 🚁 LethaboDrone: Autonomous Drone Navigation with Reinforcement Learning

![Unity](https://img.shields.io/badge/Unity-6-000000?logo=unity&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white)
![ML-Agents](https://img.shields.io/badge/ML--Agents-PPO-0A66C2)
![Python](https://img.shields.io/badge/Python-3-3776AB?logo=python&logoColor=white)

A hovering drone that teaches itself to fly across procedurally generated hilly terrain to a target, avoiding obstacles along the way. Nothing about the route is hand-coded. The drone learns purely from rewards and penalties, using **Proximal Policy Optimization (PPO)** in **Unity ML-Agents**.

## 🎥 Demo


https://github.com/user-attachments/assets/c77d7038-be13-4ec6-a937-04f21baa52bf


## 🎯 The Task

- The drone spawns in one corner of a 10 × 10 arena, and the target spawns in the opposite corner.
- **Every episode is different.** The start and target corners are randomised so the drone can't memorise a route, new Perlin-noise hills are generated, and the obstacle pillars are moved to new positions.
- A height **ceiling** stops the drone from simply flying over the pillars, so it has to navigate *around* them.
- An episode ends when the drone reaches the target, hits a pillar, crashes into the terrain, or leaves the arena.

## 🧠 How the Agent Works

### Observations (15 values + ray sensor)

| Observation | Size |
|---|---|
| Drone position (normalised) | 3 |
| Target position (normalised) | 3 |
| Drone velocity | 3 |
| Distance to target | 1 |
| Height above the ground | 1 |
| **Ground sensors**: terrain clearance 1 unit ahead, behind, left and right | 4 |
| **Ray Perception Sensor 3D**: detects nearby pillars | rays |

### Actions (3 continuous)

`[left/right, up/down, forward/back]` thrust. Gravity is cancelled in code, so an action of 0 means the drone hovers in place.

### Reward Design

| Event | Reward |
|---|---|
| Reach the target | **+1.0** |
| Move closer to / further from the target | **±0.1 × distance change** |
| Every step | **−0.001** (encourages short routes) |
| Crash into terrain or leave the arena | **−0.5** |
| Hit an obstacle | **−1.0** |

## 🧪 Experiments

### Hypothesis 1: Do ground sensors help?

I trained two agents with identical settings. The only difference was the `useGroundSensors` toggle. Results are averaged over the last 10 logged training points:

| Run | Ground sensors | Success rate | Hit obstacle | Crashed into terrain / left arena | Mean reward |
|---|---|---|---|---|---|
| `Baseline` | ✅ On | **69.5%** | 21.6% | 8.9% | **0.67** |
| `NoSensors` | ❌ Off | 51.0% | 13.0% | 35.9% | 0.01 |

**Result: hypothesis supported.** With ground sensors, the drone reached the target **18.5 percentage points more often**, and its terrain crashes dropped from about 36% to about 9%. Without them, the drone couldn't see the hills coming and flew into them.

Interestingly, the NoSensors agent hit *fewer* pillars. That's not because it avoided them better: many of its episodes ended in a terrain crash before it ever reached the pillars.

> **Caveat:** the NoSensors run trained for about 216k steps, while Baseline trained for 500k, so part of the gap may come from less training. A fair rerun would train both for 500k steps.
### Curriculum learning

`lethabo_drone_curriculum.yaml` trains the drone on progressively harder terrain. It moves to the next lesson once the smoothed reward reaches 0.6:

| Lesson | Hill height |
|---|---|
| Gentle | 0.05 |
| Medium | 0.15 |
| Full | 0.30 |

## ⚙️ Training Setup

| Setting | Value |
|---|---|
| Algorithm | PPO |
| Network | 2 layers × 128 units, normalised inputs |
| Learning rate | 3e-4 (linear decay) |
| Batch / buffer size | 1024 / 10240 |
| Discount (γ) | 0.99 |
| Training steps | 500,000 |

## 🚀 Running It

### Watch the trained drone

1. Open the project in **Unity 6**.
2. Open `Assets/Scenes/NavScene.unity`.
3. Press **Play**. The trained model (`Assets/BaselineModel.onnx`) flies the drone.

To fly it yourself, set **Behavior Type** to *Heuristic Only* on the agent and use **W/A/S/D** to move, **Space** to go up and **Left Shift** to go down.

### Train from scratch

```bash
python -m venv venv
source venv/bin/activate
pip install mlagents

mlagents-learn lethabo_drone.yaml --run-id=Baseline
# or with curriculum learning:
mlagents-learn lethabo_drone_curriculum.yaml --run-id=Curriculum
```

Press **Play** in Unity when prompted. To watch training progress, including the custom `Nav/SuccessRate` and `Nav/ObstacleHits` stats:

```bash
tensorboard --logdir results
```

## 📁 Project Structure

```
Assets/
├── Scripts/
│   ├── LethaboDroneAgent.cs   # ML-Agents agent: observations, actions, rewards, terrain generation
│   ├── DroneHUD.cs            # On-screen stats (episodes, successes, obstacle hits)
│   ├── TerrainPainter.cs      # Colours the terrain whenever new hills are generated
│   ├── CameraOrbit.cs         # Orbiting camera
│   ├── ArenaDecor.cs, Spinner.cs
├── Scenes/NavScene.unity      # Training / demo scene
└── BaselineModel.onnx         # Trained policy
lethabo_drone.yaml             # PPO training config
lethabo_drone_curriculum.yaml  # PPO + curriculum config
```

## 👤 Author

**Lethabo**, Computer Systems Engineering student at Tshwane University of Technology
