import { t } from './i18n.js';

const setElementText = (id, text) => {
  const el = document.getElementById(id);
  if (el) el.innerText = text;
};

setElementText('panel-title', t('panelTitle'));
setElementText('status-box', t('readyToDispatch'));
setElementText('refresh-jobs-btn', t('refreshJobs'));

// Map Initialisierung
const germanyBounds = L.latLngBounds(L.latLng(46.5, 4.5), L.latLng(55.8, 16.0));
const map = L.map('map', {
  center: [51.1657, 10.4515],
  zoom: 6,
  minZoom: 5,
  maxZoom: 14,
  maxBounds: germanyBounds,
  maxBoundsViscosity: 0.8
});

L.tileLayer('https://tile.openstreetmap.de/{z}/{x}/{y}.png', {
  maxZoom: 18,
  attribution: '&copy; OpenStreetMap Deutschland'
}).addTo(map);

// Sidebar Toggle
const sidebar = document.getElementById('sidebar');
const openBtn = document.getElementById('open-sidebar-btn');
const closeBtn = document.getElementById('close-sidebar-btn');

closeBtn.onclick = () => { sidebar.classList.add('collapsed'); openBtn.style.display = 'flex'; };
openBtn.onclick = () => { sidebar.classList.remove('collapsed'); openBtn.style.display = 'none'; };

// Dashboard Modal & Tabs
const mgmtModal = document.getElementById('management-modal');
const closeMgmtBtn = document.getElementById('close-mgmt-btn');
const tabFleetBtn = document.getElementById('mgmt-tab-fleet-btn');
const tabDriverBtn = document.getElementById('mgmt-tab-driver-btn');
const tabMarketTrucksBtn = document.getElementById('mgmt-tab-market-trucks-btn');
const tabMarketDriversBtn = document.getElementById('mgmt-tab-market-drivers-btn');

const viewFleet = document.getElementById('mgmt-fleet-view');
const viewDriver = document.getElementById('mgmt-driver-view');
const viewMarketTrucks = document.getElementById('mgmt-market-trucks-view');
const viewMarketDrivers = document.getElementById('mgmt-market-drivers-view');

document.getElementById('open-fleet-hub-btn').onclick = () => openManagement('fleet');
document.getElementById('open-drivers-hub-btn').onclick = () => openManagement('drivers');
document.getElementById('open-market-hub-btn').onclick = () => openManagement('market-trucks');

function openManagement(tab) {
  mgmtModal.style.display = 'flex';
  switchMgmtTab(tab);
}

closeMgmtBtn.onclick = () => { mgmtModal.style.display = 'none'; };

function switchMgmtTab(tab) {
  const allBtns = [tabFleetBtn, tabDriverBtn, tabMarketTrucksBtn, tabMarketDriversBtn];
  const allViews = [viewFleet, viewDriver, viewMarketTrucks, viewMarketDrivers];

  allBtns.forEach(b => b.classList.remove('active'));
  allViews.forEach(v => v.style.display = 'none');

  if (tab === 'fleet') {
    tabFleetBtn.classList.add('active');
    viewFleet.style.display = 'grid';
    document.getElementById('mgmt-title').innerText = "Flotten-Management";
    document.getElementById('mgmt-subtitle').innerText = "Fahrzeugstatus, Tankfüllstände und Werkstattoptionen";
  } else if (tab === 'drivers') {
    tabDriverBtn.classList.add('active');
    viewDriver.style.display = 'grid';
    document.getElementById('mgmt-title').innerText = "Personalabteilung";
    document.getElementById('mgmt-subtitle').innerText = "Angestellte Kraftfahrer, Kondition und Moral";
  } else if (tab === 'market-trucks') {
    tabMarketTrucksBtn.classList.add('active');
    viewMarketTrucks.style.display = 'grid';
    document.getElementById('mgmt-title').innerText = "LKW-Katalog & Fahrzeughandel";
    document.getElementById('mgmt-subtitle').innerText = "Erweitere deinen Fuhrpark um neue Lieferwagen und Sattelzüge";
    loadMarket();
  } else if (tab === 'market-drivers') {
    tabMarketDriversBtn.classList.add('active');
    viewMarketDrivers.style.display = 'grid';
    document.getElementById('mgmt-title').innerText = "Arbeitsagentur & Bewerber-Pool";
    document.getElementById('mgmt-subtitle').innerText = "Qualifizierte Fahrer unter Vertrag nehmen";
    loadMarket();
  }
}

tabFleetBtn.onclick = () => switchMgmtTab('fleet');
tabDriverBtn.onclick = () => switchMgmtTab('drivers');
tabMarketTrucksBtn.onclick = () => switchMgmtTab('market-trucks');
tabMarketDriversBtn.onclick = () => switchMgmtTab('market-drivers');

let cachedState = null;
let selectedJobForDispatch = null;
const mapTours = new Map();

function getTruckIllustration(type) {
  if (type === "SemiTruck") return 'https://images.unsplash.com/photo-1601584115197-04ecc0da31d7?auto=format&fit=crop&w=400&q=80';
  if (type === "Rigid") return 'https://images.unsplash.com/photo-1519003722824-194d4455a60c?auto=format&fit=crop&w=400&q=80';
  return 'https://images.unsplash.com/photo-1541899481282-d53bffe3c35d?auto=format&fit=crop&w=400&q=80';
}

// 1. Frachtaufträge laden
export async function loadJobs() {
  const container = document.getElementById('job-list');
  container.innerHTML = "Lade Frachtbörse...";
  
  const res = await fetch('/api/jobs');
  const jobs = await res.json();
  
  container.innerHTML = "";
  jobs.forEach(job => {
    const card = document.createElement('div');
    card.className = `job-card ${job.isIllegal ? 'illegal' : 'legal'}`;
    
    card.innerHTML = `
      <div class="job-title" style="color: ${job.isIllegal ? '#ff5252' : '#e0e0e0'}">${job.title}</div>
      <div>${job.originCity} ➔ ${job.destinationCity} (${job.cargoWeightTons} t)</div>
      <div class="job-meta">
        <span style="color: #ffd166; font-weight: bold;">+${job.revenue.toLocaleString('de-DE')} €</span>
        ${job.isIllegal ? `<span style="color: #ff5252;">Risiko: ${job.inspectionRiskPercentage}%</span>` : '<span style="color: #69f0ae;">Legal</span>'}
      </div>
    `;
    card.onclick = () => openDispatchModal(job);
    container.appendChild(card);
  });
}

// 2. Dispatch Modal
function openDispatchModal(job) {
  if (!cachedState) return;
  selectedJobForDispatch = job;

  document.getElementById('modal-job-title').innerText = job.title;
  document.getElementById('modal-job-details').innerHTML = `
    Route: <b>${job.originCity} ➔ ${job.destinationCity}</b><br>
    Frachtgewicht: <b>${job.cargoWeightTons} t</b> | Vergütung: <b style="color: #ffd166;">+${job.revenue.toLocaleString('de-DE')} €</b>
  `;

  const truckSelect = document.getElementById('select-truck');
  truckSelect.innerHTML = cachedState.trucks.map(t => {
    const isIdle = t.status === "Idle";
    const hasPayload = t.maxPayloadTons >= job.cargoWeightTons;
    const disabled = !isIdle || !hasPayload;
    const label = `${t.licensePlate} (${t.modelName}) | Max: ${t.maxPayloadTons}t | Tank: ${Math.round(t.currentFuelLiters)}L ${!isIdle ? `[${t.status}]` : !hasPayload ? '[Zu schwer]' : ''}`;
    return `<option value="${t.id}" ${disabled ? 'disabled' : ''}>${label}</option>`;
  }).join('');

  const driverSelect = document.getElementById('select-driver');
  driverSelect.innerHTML = cachedState.drivers.map(d => {
    const isAvail = d.status === "Available";
    const label = `${d.name} | Skill: ${d.drivingSkill} | Moral: ${d.morale}% ${!isAvail ? `[${d.status}]` : ''}`;
    return `<option value="${d.id}" ${!isAvail ? 'disabled' : ''}>${label}</option>`;
  }).join('');

  document.getElementById('modal-warning').innerText = "";
  document.getElementById('dispatch-modal').style.display = 'flex';
}

function closeDispatchModal() {
  document.getElementById('dispatch-modal').style.display = 'none';
  selectedJobForDispatch = null;
}

document.getElementById('close-modal-btn').onclick = closeDispatchModal;
document.getElementById('cancel-dispatch-btn').onclick = closeDispatchModal;

document.getElementById('confirm-dispatch-btn').onclick = async () => {
  const truckId = document.getElementById('select-truck').value;
  const driverId = document.getElementById('select-driver').value;
  const warning = document.getElementById('modal-warning');

  if (!truckId || !driverId) {
    warning.innerText = "Bitte wähle ein geeignetes Fahrzeug und einen Fahrer aus.";
    return;
  }

  // Endpunkt erwartet JobId, TruckId, DriverId laut GameEndpoints.cs
  const res = await fetch('/api/tours/dispatch', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      jobId: selectedJobForDispatch.id,
      truckId: truckId,
      driverId: driverId
    })
  });

  if (!res.ok) {
    const err = await res.text();
    warning.innerText = err;
    return;
  }

  closeDispatchModal();
  loadJobs();
};

// 3. Marktplatz Daten laden (/api/market)
async function loadMarket() {
  try {
    const res = await fetch('/api/market');
    if (!res.ok) return;
    const market = await res.json();

    // LKW-Katalog rendern
    viewMarketTrucks.innerHTML = market.trucks.map(t => {
      const truckImg = getTruckIllustration(t.type);
      return `
        <div class="mgmt-card">
          <div class="mgmt-card-hero">
            <img src="${truckImg}" alt="${t.modelName}" />
            <span class="mgmt-badge" style="background:#0284c7; color:#fff;">Neufahrzeug</span>
          </div>
          <div class="mgmt-card-body">
            <div>
              <div class="mgmt-card-title">${t.modelName}</div>
              <div class="mgmt-card-sub">${t.type} · Max. Zuladung: ${t.maxPayloadTons}t</div>
            </div>
            <div style="font-size: 18px; font-weight: bold; color: #ffd166; margin: 4px 0;">
              ${t.price.toLocaleString('de-DE')} €
            </div>
            <div class="mgmt-actions">
              <button class="btn-primary" style="margin: 0; width: 100%;" onclick="buyTruck('${t.catalogId}')">
                Kaufen & Einflotten
              </button>
            </div>
          </div>
        </div>
      `;
    }).join('');

    // Fahrer-Bewerberpool rendern
    viewMarketDrivers.innerHTML = market.drivers.map(d => {
      const avatarUrl = `https://api.dicebear.com/7.x/bottts/svg?seed=${encodeURIComponent(d.name)}`;
      return `
        <div class="mgmt-card">
          <div class="mgmt-card-hero">
            <img src="${avatarUrl}" class="avatar-circle" alt="${d.name}" />
            <span class="mgmt-badge badge-idle">Bewerber</span>
          </div>
          <div class="mgmt-card-body">
            <div>
              <div class="mgmt-card-title">${d.name}</div>
              <div class="mgmt-card-sub">Gehaltsanspruch: ${d.expectedSalary.toLocaleString('de-DE')} €/Monat</div>
            </div>
            <div class="stat-row">
              <div class="stat-header"><span>Fahrpraxis</span><span>${d.drivingSkill}/100</span></div>
              <div class="progress-bar-bg"><div class="progress-bar-fill bg-skill" style="width: ${d.drivingSkill}%"></div></div>
            </div>
            <div class="stat-row">
              <div class="stat-header"><span>Zuverlässigkeit</span><span>${d.reliability}/100</span></div>
              <div class="progress-bar-bg"><div class="progress-bar-fill bg-engine" style="width: ${d.reliability}%"></div></div>
            </div>
            <div class="stat-row">
              <div class="stat-header"><span>Loyalität</span><span>${d.loyalty}/100</span></div>
              <div class="progress-bar-bg"><div class="progress-bar-fill bg-fuel" style="width: ${d.loyalty}%"></div></div>
            </div>
            <div class="mgmt-actions">
              <button class="btn-primary" style="margin: 0; width: 100%;" onclick="hireDriver('${d.id}')">
                Einstellen
              </button>
            </div>
          </div>
        </div>
      `;
    }).join('');

  } catch (err) {
    console.error("Fehler beim Laden des Marktes:", err);
  }
}

// Aktionen: Kauf, Einstellung, Tanken, Wartung
window.buyTruck = async (catalogId) => {
  const res = await fetch('/api/fleet/buy', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ catalogId })
  });
  if (!res.ok) { alert(await res.text()); return; }
  switchMgmtTab('fleet');
};

window.hireDriver = async (prospectId) => {
  const res = await fetch('/api/personnel/hire', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ prospectId })
  });
  if (!res.ok) { alert(await res.text()); return; }
  switchMgmtTab('drivers');
};

window.refuelTruck = async (truckId) => {
  const res = await fetch('/api/fleet/refuel', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ truckId })
  });
  if (!res.ok) alert(await res.text());
};

window.maintainTruck = async (truckId, level) => {
  const res = await fetch('/api/fleet/maintain', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ truckId, level })
  });
  if (!res.ok) alert(await res.text());
};

// 4. Live GameState Synchronisation
async function syncGameState() {
  try {
    const res = await fetch('/api/state');
    if (!res.ok) return;

    const data = await res.json();
    cachedState = data;

    document.getElementById('balance-box').innerText = `Kontostand: ${data.balance.toLocaleString('de-DE', { minimumFractionDigits: 2 })} €`;
    document.getElementById('status-box').innerText = `Aktive Touren: ${data.activeTours.length} | Tag ${data.day}, ${data.hour}:00 Uhr`;
    document.getElementById('log-list').innerHTML = data.logs.map(log => `<div class="log-entry">${log}</div>`).join('');

    // Eigene Flotte rendern
    viewFleet.innerHTML = data.trucks.map(t => {
      const isIdle = t.status === "Idle";
      const fuelPct = Math.min(100, (t.currentFuelLiters / t.fuelCapacityLiters) * 100);
      const badgeClass = isIdle ? 'badge-idle' : t.status === "Impounded" ? 'badge-danger' : 'badge-active';
      const truckImg = getTruckIllustration(t.type);

      return `
        <div class="mgmt-card">
          <div class="mgmt-card-hero">
            <img src="${truckImg}" alt="${t.modelName}" />
            <span class="mgmt-badge ${badgeClass}">${t.status}</span>
          </div>
          <div class="mgmt-card-body">
            <div>
              <div class="mgmt-card-title">${t.licensePlate} · ${t.modelName}</div>
              <div class="mgmt-card-sub">${t.type} · Max. Zuladung: ${t.maxPayloadTons}t · Standort: ${t.currentCity}</div>
            </div>
            <div class="stat-row">
              <div class="stat-header"><span>Diesel</span><span>${Math.round(t.currentFuelLiters)} / ${t.fuelCapacityLiters} L (${fuelPct.toFixed(0)}%)</span></div>
              <div class="progress-bar-bg"><div class="progress-bar-fill bg-fuel" style="width: ${fuelPct}%"></div></div>
            </div>
            <div class="stat-row">
              <div class="stat-header"><span>Motor</span><span>${t.engineCondition.toFixed(1)}%</span></div>
              <div class="progress-bar-bg"><div class="progress-bar-fill bg-engine" style="width: ${t.engineCondition}%"></div></div>
            </div>
            <div class="stat-row">
              <div class="stat-header"><span>Reifen</span><span>${t.tireCondition.toFixed(1)}%</span></div>
              <div class="progress-bar-bg"><div class="progress-bar-fill bg-tire" style="width: ${t.tireCondition}%"></div></div>
            </div>
            <div class="mgmt-actions">
              <button class="mgmt-action-btn" onclick="refuelTruck('${t.id}')" ${!isIdle ? 'disabled' : ''}>⛽ Tanken</button>
              <button class="mgmt-action-btn" onclick="maintainTruck('${t.id}', 1)" ${!isIdle ? 'disabled' : ''}>🔧 Wartung</button>
            </div>
          </div>
        </div>
      `;
    }).join('');

    // Eigenes Personal rendern
    viewDriver.innerHTML = data.drivers.map(d => {
      const isAvail = d.status === "Available";
      const isSick = d.status === "SickLeave";
      const badgeClass = isAvail ? 'badge-idle' : isSick ? 'badge-danger' : 'badge-active';
      const avatarUrl = `https://api.dicebear.com/7.x/bottts/svg?seed=${encodeURIComponent(d.name)}`;

      return `
        <div class="mgmt-card">
          <div class="mgmt-card-hero">
            <img src="${avatarUrl}" class="avatar-circle" alt="${d.name}" />
            <span class="mgmt-badge ${badgeClass}">${d.status}</span>
          </div>
          <div class="mgmt-card-body">
            <div>
              <div class="mgmt-card-title">${d.name}</div>
              <div class="mgmt-card-sub">Lohn: ${d.currentSalary.toLocaleString('de-DE')} €/Monat</div>
            </div>
            <div class="stat-row">
              <div class="stat-header"><span>Fahrpraxis</span><span>${d.drivingSkill}/100</span></div>
              <div class="progress-bar-bg"><div class="progress-bar-fill bg-skill" style="width: ${d.drivingSkill}%"></div></div>
            </div>
            <div class="stat-row">
              <div class="stat-header"><span>Moral</span><span>${d.morale}%</span></div>
              <div class="progress-bar-bg"><div class="progress-bar-fill bg-fuel" style="width: ${d.morale}%"></div></div>
            </div>
            <div class="stat-row">
              <div class="stat-header"><span>Gesundheit</span><span>${d.health}%</span></div>
              <div class="progress-bar-bg"><div class="progress-bar-fill bg-engine" style="width: ${d.health}%"></div></div>
            </div>
          </div>
        </div>
      `;
    }).join('');

    // Touren auf Karte
    const activeServerIds = new Set(data.activeTours.map(t => t.id));
    for (const [id, tourObj] of mapTours.entries()) {
      if (!activeServerIds.has(id)) {
        map.removeLayer(tourObj.polyline);
        map.removeLayer(tourObj.marker);
        mapTours.delete(id);
      }
    }

    data.activeTours.forEach(tour => {
      let tourObj = mapTours.get(tour.id);
      if (!tourObj) {
        const latLngs = tour.fullGeometry.map(p => [p.latitude, p.longitude]);
        const polyline = L.polyline(latLngs, {
          color: tour.isIllegal ? '#ff5252' : '#00bcd4',
          weight: 4
        }).addTo(map);

        const marker = L.circleMarker([tour.currentPoint.latitude, tour.currentPoint.longitude], {
          radius: 6,
          color: '#ffffff',
          fillColor: tour.isIllegal ? '#ff1744' : '#00e676',
          fillOpacity: 1,
          weight: 2
        }).addTo(map).bindPopup(`${tour.truckPlate} (${tour.driverName})<br>${tour.title}`);

        tourObj = { polyline, marker };
        mapTours.set(tour.id, tourObj);
      } else if (tour.currentPoint) {
        tourObj.marker.setLatLng([tour.currentPoint.latitude, tour.currentPoint.longitude]);
      }
    });

  } catch (err) {
    console.error("Sync error:", err);
  }
}

document.getElementById('refresh-jobs-btn').onclick = async () => {
  await fetch('/api/jobs/refresh', { method: 'POST' });
  loadJobs();
};

loadJobs();
setInterval(syncGameState, 1000);