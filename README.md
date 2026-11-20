# 🏎️ Virtual Driving School & Manual Transmission Simulator (3D)

A realistic 3D virtual driving simulation built for Unity, designed to teach manual driving, parallel and 90-degree bay parking, precision mirror backing, and clutch bite-point control with full native support for the **Logitech G920 Driving Force Racing Wheel, 3-Pedal Set, and 6-Speed Shifter**.

---

## 🎮 Logitech G920 Controller Mappings

| Control Element | Logitech G920 Input | Function in Simulator |
| :--- | :--- | :--- |
| **Steering Wheel** | Wheel Rotation (up to 900°) | Steers front wheels with realistic Ackermann geometry |
| **Throttle Pedal** | Right Pedal (Axis 2) | Accelerator opening with normalized sensitivity |
| **Brake Pedal** | Middle Pedal (Axis 3) | Progressive hydraulic foot brake with front bias |
| **Clutch Pedal** | Left Pedal (Axis 4/6) | Manual clutch with simulated friction bite point |
| **H-Shifter** | Slots 1–6 (Buttons 12–17) | 1st, 2nd, 3rd, 4th, 5th, 6th manual gears |
| **Reverse Gear** | Push knob down + 6th slot (Button 18) | Reverse gear (-3.8:1 gear ratio) |
| **Neutral** | Center stick slot | Disengages engine torque from drive shaft |
| **Handbrake** | Button **A** (Button 0) | Mechanical parking brake (locks rear wheels) |
| **Starter Ignition** | Button **X** (Button 2) | Cranks starter motor (requires clutch in / neutral) |
| **Horn** | Button **B** (Button 1) | Audible vehicle horn |
| **Camera View** | Button **Y** (Button 3) | Toggles Cockpit ➔ Chase ➔ Hood ➔ Overhead Parking |
| **Paddle Shifters**| **RB** (Upshift) / **LB** (Downshift) | Sequential gear shift fallback |
| **Turn Signals** | D-Pad Left / D-Pad Right | Activates dashboard and exterior blinkers |
| **Hazard Lights** | D-Pad Down | Emergency 4-way flashers |
| **Reset Vehicle** | View / Back (Button 6) | Respawns car to lesson starting point |

---

## 🛞 Connecting a Logitech Wheel & Pedals

1. Install **Logitech G HUB** so Windows sees the G920/G923/G29 as a wheel, then plug the wheel and pedals in **before** pressing Play.
2. The first time a wheel is detected, an on-screen **Wheel & Pedal Setup** opens: center the wheel and press Enter (or A), turn fully left, fully right, then press and release gas, brake and clutch (Space skips the clutch). Each step advances after holding about a second.
3. The calibration is saved on that computer. Press **F9** any time to recalibrate, Esc to cancel.
4. Gears: the H-shifter is used as soon as you move it; the paddles (RB up, LB down) shift sequentially and take over again when pressed.
5. No wheel? The keyboard controls below work automatically.

---

## ⌨️ Keyboard & Mouse Fallback Controls

You can also practice immediately without a steering wheel plugged in:

| Action | Keyboard Key |
| :--- | :--- |
| **Steer Left / Right** | `A` / `D` or `Left Arrow` / `Right Arrow` |
| **Throttle (Gas)** | `W` or `Up Arrow` |
| **Brake** | `S` or `Down Arrow` |
| **Clutch Pedal** | `Left Shift` *(Hold to disengage clutch, release to engage)* |
| **Gears 1 through 6** | Number keys `1`, `2`, `3`, `4`, `5`, `6` |
| **Neutral** | `N` |
| **Reverse** | `R` |
| **Handbrake** | `Spacebar` |
| **Engine Starter** | `E` |
| **Horn** | `F` |
| **Cycle Camera** | `C` |
| **Glance Left / Right**| `Q` / `E` |
| **Look Behind** | `B` |
| **Turn Signals** | `[` (Left) / `]` (Right) |
| **Hazard Lights** | `H` |
| **Reset Vehicle** | `Backspace` |

---

## ⚙️ Manual Transmission & Engine Physics

1. **Clutch Bite Point (Friction Zone):**
   - The HUD pedal visualizer displays a gold highlighted band between **35% and 65% travel**.
   - In this zone, clutch plates begin to lock up. Releasing the pedal smoothly transfers torque to the wheels.
2. **Realistic Engine Stall:**
   - If the clutch is released too quickly from a dead stop without adequate gas, the engine RPM drops below **450 RPM** and the engine violently shudders and stalls!
   - To recover from a stall: depress the clutch pedal fully (or shift to Neutral) and press the Starter key (`E` / Button `X`).
3. **Gear Grinding:**
   - Attempting to force the car into gear while moving without depressing the clutch produces a loud transmission gear grind penalty!
4. **Engine Braking:**
   - Releasing the throttle in lower gears creates vacuum drag and compression braking, naturally slowing the car.

---

## 🎓 Structured Driving Curriculum

- **Lesson 1: The Manual Basics & Clutch Bite Point**
  - Learn to start the ignition safely, engage 1st gear, slowly ease the clutch into the friction zone to creep forward without stalling, add throttle, and brake smoothly.
- **Lesson 2: Upshifting & Downshifting (1st to 3rd)**
  - Accelerate to 15 MPH, shift to 2nd, accelerate to 25 MPH, shift to 3rd, brake smoothly, and downshift.
- **Lesson 3: Reversing & Mirror Backing**
  - Engage reverse gear, modulate low speed with the clutch, and back down a narrow cone corridor into a designated bay using the rear-view and side mirror cameras.
- **Lesson 4: 90° Perpendicular Bay Parking**
  - Approach the parking lot, swing into an open bay between obstacle cars, center between lines, shift to Neutral, and engage the handbrake.
- **Lesson 5: Parallel Parking Between Cars**
  - Pull up next to the lead car, engage reverse, cut the wheel at 45°, counter-steer into the curb, and center within 1 foot of the curb.
- **Lesson 6: Hill Start (Anti-Rollback Challenge)**
  - Stop on the 14° incline ramp. Use the handbrake to hold position, bring the clutch to the bite point until the engine note deepens, then release the handbrake and launch with zero rollback!
- **Free Roam Mode:**
  - Open sandbox practice across the parking lot, cone slalom course, perimeter 2-lane roads, and 4-way stop sign intersections.

---

## 🚀 How to Open in Unity

1. Launch **Unity Hub**.
2. Click **Open** ➔ **Add project from disk**.
3. Select the folder:
   ```
   C:\Users\respi\.gemini\antigravity\scratch\VirtualDrivingSchool
   ```
4. Open the project with **Unity 6 (6000.3.28f1)** or **Unity 2022.3 LTS**.
5. In the Project window, double-click:
   `Assets/Scenes/DrivingSchool_Main.unity`
6. Click the **Play** button in Unity!
   *(Or click toolbar menu `Driving Simulator -> Build Complete Driving School Scene` to inspect all objects in edit mode).*
