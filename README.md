# 🏃 Catch & Run! — 2D Local Multiplayer Action

[![Unity](https://img.shields.io/badge/Unity-2022.3%2B-black?style=for-the-badge&logo=unity)](https://unity.com/)
[![C#](https://img.shields.io/badge/C%23-10.0-239120?style=for-the-badge&logo=c-sharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Networking](https://img.shields.io/badge/Networking-LAN%20%2F%20Host--Client-blue?style=for-the-badge)](https://unity.com/)
[![Platform](https://img.shields.io/badge/Platform-PC%20%2F%20Windows-lightgrey?style=for-the-badge&logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

**Catch & Run!** is a high-octane, local network (LAN) 2D multiplayer tag game developed with Unity and C#. Players battle across dynamic arena maps where taggers chase runners, leveraging real-time environmental interactions, throwable object physics, stun mechanics, and tactical movement to survive before the round timer expires.

---

## 📸 Media & In-Game Showcase

<p align="center">
  <img src="Assets/20250510_1507_Press%20to%20Play_simple_compose_01jtx1fd9fe50tqqnw6de8z4xw.png" alt="Catch and Run Start Screen" width="750"/>
</p>

### Lobby & Match Flow
<p align="center">
  <img src="Assets/EndPahe.png" alt="Catch and Run Match End Screen" width="750"/>
</p>

---

## 🌟 Key Technical Features

### 1. 🔄 Host-Client LAN Networking & State Synchronization
* **Network Transform & Rigidbody Sync:** Low-latency player position, velocity, and rotation interpolation across local network sockets.
* **Role State Authority:** Authoritative server-side validation for tagging logic to eliminate latency-induced phantom hits.
* **Networked Game Loop:** Synchronized match countdowns, round timers, dynamic role assignment, and win/loss resolution.

### 2. 🎯 Physics-Driven Throw & Stun Mechanics
* **Interactive Pickups:** Runners and taggers can pick up arena objects and dynamically aim with directional velocity vectors.
* **Stun System:** Successful projectile impacts apply an extensible status effect (`IStunnable`), interrupting movement input and physics forces for a balanced recovery window.

### 3. 🏃 Agile 2D Movement Architecture
* **Responsive Kinematics:** Custom raycast-assisted ground and obstacle detection combined with Rigidbody2D velocity damping for tight controls.
* **Input Decoupling:** Player input is separated from character controllers via clean C# event delegates, enabling seamless replay or AI extensions.

---

## 🏛️ Code Architecture & Design Patterns

The codebase is built on **SOLID** principles, utilizing **State Patterns**, **Service Locators/Singletons for Game Managers**, and **Observer Pattern (C# Events)** for decoupled UI and audio integration.

### Core Systems Breakdown
