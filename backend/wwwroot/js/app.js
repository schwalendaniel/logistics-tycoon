import { t } from './i18n.js';

// Statische Labels per ID nur setzen, wenn das Element existiert
const setElementText = (id, text) => {
  const el = document.getElementById(id);
  if (el) el.innerText = text;
};

setElementText('panel-title', t('panelTitle'));
setElementText('section-label', t('availableFreight'));
setElementText('status-box', t('readyToDispatch'));
setElementText('refresh-jobs-btn', t('refreshJobs'));
setElementText('ticker-label', t('latestEvents'));

// Map Initialisierung (nur einmal!)
const germanyBounds = L.latLngBounds(
  L.latLng(46.5, 4.5),
  L.latLng(55.8, 16.0)
);

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
  attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> Deutschland'
}).addTo(map);

// Sidebar Toggles
const sidebar = document.getElementById('sidebar');
const openBtn = document.getElementById('open-sidebar-btn');
const closeBtn = document.getElementById('close-sidebar-btn');

closeBtn.onclick = () => {
  sidebar.classList.add('collapsed');
  openBtn.style.display = 'flex';
};
openBtn.onclick = () => {
  sidebar.classList.remove('collapsed');
  openBtn.style.display = 'none';
};

// Tab Umschaltung
document.querySelectorAll('.tab-btn').forEach(btn => {
  btn.onclick = () => {
    document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
    document.querySelectorAll('.tab-pane').forEach(p => p.style.display = 'none');
    
    btn.classList.add('active');
    document.getElementById(`tab-${btn.dataset.tab}`).style.display = 'block';
  };
});

let cachedState = null;
let selectedJobForDispatch = null;
const mapTours = new Map();

// 1. Aufträge laden
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

// 2. Dispatch Dialog
function openDispatchModal(job) {
  if (!cachedState) return;
  selectedJobForDispatch = job;

  document.getElementById('modal-job-title').innerText = job.title;
  document.getElementById('modal-job-details').innerHTML = `
    Route: <b>${job.originCity} ➔ ${job.destinationCity}</b><br>
    Frachtgewicht: <b>${job.cargoWeightTons} t</b> | Vergütung: <b style="color: #ffd166;">+${job.revenue.toLocaleString('de-DE')} €</b>
  `;

  // LKW-Auswahl befüllen (Casing-Bug behoben: cargoWeightTons klein geschrieben)
  const truckSelect = document.getElementById('select-truck');
  truckSelect.innerHTML = cachedState.trucks.map(t => {
    const isIdle = t.status === "Idle";
    const hasPayload = t.maxPayloadTons >= job.cargoWeightTons;
    const disabled = !isIdle || !hasPayload;
    const label = `${t.licensePlate} (${t.modelName}) | Max: ${t.maxPayloadTons}t | Tank: ${Math.round(t.currentFuelLiters)}L ${!isIdle ? `[${t.status}]` : !hasPayload ? '[Zu schwer]' : ''}`;
    return `<option value="${t.id}" ${disabled ? 'disabled' : ''}>${label}</option>`;
  }).join('');

  // Fahrer-Auswahl befüllen
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

  const res = await fetch('/api/tours/dispatch', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      job: selectedJobForDispatch,
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

// 3. State Synchronisation (Jede Sekunde)
async function syncGameState() {
  try {
    const res = await fetch('/api/state');
    if (!res.ok) return;

    const data = await res.json();
    cachedState = data;

    // Header-Infos
    document.getElementById('balance-box').innerText = `Kontostand: ${data.balance.toLocaleString('de-DE', { minimumFractionDigits: 2 })} €`;
    document.getElementById('status-box').innerText = `Aktive Touren: ${data.activeTours.length}`;
    document.getElementById('log-list').innerHTML = data.logs.map(log => `<div class="log-entry">${log}</div>`).join('');

    // Fuhrpark-Tab aktualisieren
    const truckList = document.getElementById('truck-list');
    truckList.innerHTML = data.trucks.map(t => {
      const isIdle = t.status === "Idle";
      return `
        <div class="job-card">
          <div style="font-weight: bold;">${t.licensePlate} (${t.modelName})</div>
          <div>Typ: ${t.type} | Nutzlast: ${t.maxPayloadTons} t</div>
          <div>Tank: ${Math.round(t.currentFuelLiters)} / ${t.fuelCapacityLiters} L</div>
          <div>Reifen: ${t.tireCondition.toFixed(1)}% | Motor: ${t.engineCondition.toFixed(1)}%</div>
          <div style="color: ${isIdle ? '#69f0ae' : '#ffb74d'}; font-weight: bold;">Status: ${t.status}</div>
        </div>
      `;
    }).join('');

    // Fahrer-Tab aktualisieren
    const driverList = document.getElementById('driver-list');
    driverList.innerHTML = data.drivers.map(d => {
      const isAvail = d.status === "Available";
      return `
        <div class="job-card">
          <div style="font-weight: bold;">${d.name}</div>
          <div>Fahrkönnen: ${d.drivingSkill} | Loyalität: ${d.loyalty}</div>
          <div>Gesundheit: ${d.health}% | Moral: ${d.morale}%</div>
          <div style="color: ${isAvail ? '#69f0ae' : '#ffb74d'}; font-weight: bold;">Status: ${d.status}</div>
        </div>
      `;
    }).join('');

    // Touren auf Leaflet Map aktualisieren
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

document.getElementById('refresh-jobs-btn').onclick = loadJobs;
loadJobs();
setInterval(syncGameState, 1000);