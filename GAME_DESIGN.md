# Logistics Tycoon (2D Logistics Dispatcher Simulation)

## 1. Vision & Core Identity
A 2D web-based transport and fleet dispatcher simulation set on a real map of Germany. The player acts as a dispatcher/owner of a logistics company, managing routes, vehicles, drivers, finances, and risks. 
* **Style:** Modern dark-mode dispatcher dashboard with interactive maps (OpenStreetMap/Leaflet).
* **Scope:** Explicitly NO 3D simulation, NO building mechanics, and NO predatory DLCs. 

---

## 2. Technical Stack & Architecture
* **Backend:** C# / .NET 10 (ASP.NET Core Web API, Minimal APIs).
* **Database:** SQLite (Entity Framework Core) for local state and progression.
* **Routing & Maps:** OSRM (Open Source Routing Machine) API for real street geometry and driving times; Leaflet.js / OpenStreetMap on the frontend.
* **Frontend:** Lightweight modern Web UI (HTML5, CSS3, JavaScript/TypeScript, Leaflet).
* **Architecture Pattern:** Modular Feature-Slicing. Services handle distinct domains (Routing, Economy, Fleet, Tick Engine).

---

## 3. Core Gameplay Modules

### A. Fleet & Maintenance
* **Vehicle Categories:**
  * *Van (3.5t):* Agile, low fuel consumption, exempt from German truck toll (Maut), limited payload.
  * *Rigid Truck (12t):* Medium payload, regional delivery workhorse.
  * *Semi-Truck (40t):* High cargo capacity, heavy toll costs, sluggish, expensive maintenance.
* **Wear & Tear:**
  * Engine condition (0–100%) and tire condition (0–100%) degrade with driven kilometers.
  * Degraded condition increases breakdown probability, delays, and fuel usage.
  * Requires scheduled maintenance in the workshop.

### B. Driver Management (RPG Attributes)
* Every driver has distinct randomized attributes (values 0–100):
  * **Driving Skill:** Reduces fuel consumption and base accident risk.
  * **Reliability:** Affects punctuality, adherence to deadlines, and idling.
  * **Stress Resistance:** Buffer against fatigue during long hauls and tight schedules.
  * **Loyalty:** Determines whether the driver keeps silent or reports the player during illegal inspections.
* Monthly salaries scale with overall skill rating.

### C. Job Market & Disposition
* **Market Board:** Dynamic contract generation between German cities with real distance calculation.
* **Economy Formula:** Net Profit = Revenue - (Fuel Costs + Toll/Maut + Driver Wage + Wear Depreciation).
* **Logistics Optimization:** Players must minimize empty runs (deadheading) by chaining orders intelligently.

### D. The Underground Economy (Illegal Cargo & Smuggling)
* High-risk, high-reward alternative contracts (e.g., untaxed cigarettes, illegal contraband, unauthorized hazardous materials).
* **Mechanics:**
  * Significantly higher payouts compared to standard freight.
  * Percentage-based inspection risk (customs / BAG road checkpoints).
  * Checkpoint check roll:
    * If checked and driver has high **Loyalty**, driver might take the blame or evade without compromising the company.
    * If caught: Heavy financial penalties, cargo confiscated, vehicle impounded, or driver arrested.

### E. Driver Health, Compensation & "Calling in Sick"
* **Health & Morale Metrics:**
  * **Health (0–100%):** Degrades through unmaintained vehicles (dirty cabin air/moldy filters, vibration, broken AC/heating), extreme shift lengths, and poor driver stress tolerance.
  * **Morale / Satisfaction (0–100%):** Governed by compensation level (underpaid vs. fair vs. bonus pay), vehicle age/state, and workload.
* **Absence Mechanics:**
  * *Real Sickness:* Occurs when Health drops below critical thresholds. Driver is blocked for $X$ days on sick leave.
  * *Malingering ("Krankfeiern" / Calling in Sick):* High probability if Morale is critically low despite decent physical health. Drivers refuse tours spontaneously.
* **Health & Welfare Investments:**
  * Company-wide health perks (ergonomic premium seats, private supplementary health insurance, gym subsidies).
  * Direct bonus payouts or above-market wages to boost Morale and Loyalty.

---

### F. Multi-Tier Fleet Maintenance & Cabin Environment
Instead of a simple repair button, workshops offer three service tiers affecting both truck longevity and driver well-being:
* **Tier 1: "TÜV-Notdurft" (Patch Job / Minimal):**
  * Lowest cost. Repairs only critical safety components (brakes, bald tires).
  * High residual wear; engine efficiency remains suboptimal; neglects cabin components.
* **Tier 2: "Standard-Inspektion" (Standard Service):**
  * Balanced cost. Oil change, brake replacement, standard wear reset.
  * Normal wear degradation rate.
* **Tier 3: "Premium-Service" (Full Overhaul & Climate/Filter Care):**
  * High cost. Complete fluid flushes, mechanical overhaul, plus fresh cabin air/pollen filters and AC sanitization.
  * Grants a temporary boost to driver Health recovery and Morale while driving this specific vehicle.

---

## 4. Game Loop & Progression
* **Tick Engine:** A background service executes game ticks (e.g., every 1–5 seconds) to advance truck positions along OSRM route polylines, consume fuel, check random events (breakdowns, weather, inspections), and credit completed orders.
* **Progression:** Start with one cheap van and a low-skilled driver -> Expand fleet, establish regional depots, hire better personnel, and unlock heavier haulers or underground networks.

---

## 5. Development Guidelines for AI / CLI
* Generate clean, modular C# code strictly adhering to standard .NET conventions.
* Keep classes isolated; favor dependency injection.
* Avoid massive boilerplate; deliver focused, testable increments per prompt.