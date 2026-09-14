import { t } from './i18n.js';

const setElementText = (id, text) => {
  const el = document.getElementById(id);
  if (el) el.innerText = text;
};

setElementText('panel-title', t('panelTitle'));
setElementText('status-box', t('readyToDispatch'));
setElementText('refresh-jobs-btn', t('refreshJobs'));

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
  attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> Deutschland'
}).addTo(map);

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

document.querySelectorAll('.tab-btn').forEach(btn => {
  btn.onclick = () => {
    document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
    document.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('active'));
    btn.classList.add('active');
    document.getElementById(`tab-${btn.dataset.tab}`).classList.add('active');
    if (btn.dataset.tab === 'market') loadMarket();
  };
});

let cachedState = null;
let selectedJobForDispatch = null;
const mapTours = new Map();
const depotMarkers = new Map();
let lastFleetKey = '';
let lastDriverKey = '';
let lastMarketKey = '';

async function postJson(url, body) {
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  });
  if (!res.ok) {
    throw new Error(await res.text());
  }
  return res;
}

export async function loadJobs() {
  const container = document.getElementById('job-list');
  container.innerHTML = 'Lade Frachtbörse...';

  const res = await fetch('/api/jobs');
  const jobs = await res.json();

  container.innerHTML = '';
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
    const isIdle = t.status === 'Idle';
    const hasPayload = t.maxPayloadTons >= job.cargoWeightTons;
    const disabled = !isIdle || !hasPayload;
    const deadhead = t.currentCity && t.currentCity !== job.originCity ? ` | steht in ${t.currentCity}` : '';
    const label = `${t.licensePlate} (${t.modelName}) | Max: ${t.maxPayloadTons}t | Tank: ${Math.round(t.currentFuelLiters)}L${deadhead} ${!isIdle ? `[${t.status}]` : !hasPayload ? '[Zu schwer]' : ''}`;
    return `<option value="${t.id}" ${disabled ? 'disabled' : ''}>${label}</option>`;
  }).join('');

  const driverSelect = document.getElementById('select-driver');
  driverSelect.innerHTML = cachedState.drivers.map(d => {
    const isAvail = d.status === 'Available';
    const label = `${d.name} | Skill: ${d.drivingSkill} | Loyalität: ${d.loyalty} | Moral: ${d.morale}% ${!isAvail ? `[${d.status}]` : ''}`;
    return `<option value="${d.id}" ${!isAvail ? 'disabled' : ''}>${label}</option>`;
  }).join('');

  const warn = [];
  if (job.isIllegal) {
    warn.push('Schwarzmarkt: Kontrolle möglich. Niedrige Loyalität ist gefährlich.');
  }
  document.getElementById('modal-warning').innerText = warn.join(' ');
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

  if (!truckId || !driverId || !selectedJobForDispatch) {
    warning.innerText = 'Bitte wähle ein geeignetes Fahrzeug und einen Fahrer aus.';
    return;
  }

  const driver = cachedState.drivers.find(d => d.id === driverId);
  if (selectedJobForDispatch.isIllegal && driver && driver.loyalty < 50) {
    warning.innerText = `${driver.name} hat nur ${driver.loyalty} Loyalität — hohe Gefahr bei einer Razzia.`;
  }

  try {
    await postJson('/api/tours/dispatch', {
      jobId: selectedJobForDispatch.id,
      truckId,
      driverId
    });
    closeDispatchModal();
    loadJobs();
  } catch (err) {
    warning.innerText = err.message;
  }
};

document.getElementById('truck-list').addEventListener('click', async (e) => {
  const btn = e.target.closest('[data-action]');
  if (!btn) return;
  const truckId = btn.dataset.id;
  try {
    if (btn.dataset.action === 'refuel') await postJson('/api/fleet/refuel', { truckId });
    if (btn.dataset.action === 'maintain') await postJson('/api/fleet/maintain', { truckId, level: btn.dataset.level });
    if (btn.dataset.action === 'bail') await postJson('/api/fleet/bail', { truckId });
    lastFleetKey = '';
    syncGameState();
  } catch (err) {
    alert(err.message);
  }
});

document.getElementById('driver-list').addEventListener('click', async (e) => {
  const btn = e.target.closest('[data-action]');
  if (!btn) return;
  const driverId = btn.dataset.id;
  try {
    if (btn.dataset.action === 'bonus') {
      const amount = Number(prompt('Bonus in €', '250'));
      if (!amount) return;
      await postJson('/api/personnel/bonus', { driverId, amount });
    }
    if (btn.dataset.action === 'salary') {
      const monthlySalary = Number(prompt('Neues Monatsgehalt', btn.dataset.salary));
      if (!monthlySalary) return;
      await postJson('/api/personnel/salary', { driverId, monthlySalary });
    }
    if (btn.dataset.action === 'bail-driver') {
      await postJson('/api/personnel/bail', { driverId });
    }
    lastDriverKey = '';
    syncGameState();
  } catch (err) {
    alert(err.message);
  }
});

async function loadMarket() {
  const res = await fetch('/api/market');
  if (!res.ok) return;
  const data = await res.json();
  const key = JSON.stringify(data);
  if (key === lastMarketKey) return;
  lastMarketKey = key;

  document.getElementById('market-trucks').innerHTML = data.trucks.map(c => `
    <div class="job-card static">
      <div style="font-weight:bold;">${c.modelName}</div>
      <div>${c.type} | ${c.maxPayloadTons} t | ${c.price.toLocaleString('de-DE')} €</div>
      <div class="card-actions">
        <button class="btn-tiny" data-buy-truck="${c.catalogId}">Kaufen</button>
      </div>
    </div>
  `).join('');

  document.getElementById('market-drivers').innerHTML = data.drivers.map(d => `
    <div class="job-card static">
      <div style="font-weight:bold;">${d.name}</div>
      <div>Skill ${d.drivingSkill} · Zuverl. ${d.reliability} · Stress ${d.stressResistance} · Loyal ${d.loyalty}</div>
      <div>${d.askingSalary.toLocaleString('de-DE')} €/Monat · Handgeld ${d.signingFee.toLocaleString('de-DE')} €</div>
      <div class="card-actions">
        <button class="btn-tiny" data-hire="${d.id}">Einstellen</button>
      </div>
    </div>
  `).join('');

  document.getElementById('market-depots').innerHTML = data.depots.length
    ? data.depots.map(d => `
      <div class="job-card static">
        <div style="font-weight:bold;">Depot ${d.cityName}</div>
        <div>${d.price.toLocaleString('de-DE')} €</div>
        <div class="card-actions">
          <button class="btn-tiny" data-buy-depot="${d.cityName}">Kaufen</button>
        </div>
      </div>
    `).join('')
    : '<div class="job-card static">Alle angebotenen Depots gehören dir.</div>';
}

document.getElementById('tab-market').addEventListener('click', async (e) => {
  try {
    if (e.target.dataset.buyTruck) {
      await postJson('/api/fleet/buy', { catalogId: e.target.dataset.buyTruck });
      lastMarketKey = '';
      lastFleetKey = '';
      await loadMarket();
      syncGameState();
    }
    if (e.target.dataset.hire) {
      await postJson('/api/personnel/hire', { prospectId: e.target.dataset.hire });
      lastMarketKey = '';
      lastDriverKey = '';
      await loadMarket();
      syncGameState();
    }
    if (e.target.dataset.buyDepot) {
      await postJson('/api/depots/buy', { cityName: e.target.dataset.buyDepot });
      lastMarketKey = '';
      await loadMarket();
      syncGameState();
    }
  } catch (err) {
    alert(err.message);
  }
});

document.getElementById('refresh-hire-btn').onclick = async () => {
  await postJson('/api/market/hire-refresh', {});
  lastMarketKey = '';
  loadMarket();
};

async function syncGameState() {
  try {
    const res = await fetch('/api/state');
    if (!res.ok) return;

    const data = await res.json();
    cachedState = data;

    document.getElementById('balance-box').innerText =
      `Kontostand: ${data.balance.toLocaleString('de-DE', { minimumFractionDigits: 2 })} €`;
    document.getElementById('clock-box').innerText = `Tag ${data.day}, ${String(data.hour).padStart(2, '0')}:00 · Heimat ${data.homeCity}`;
    document.getElementById('status-box').innerText = `Aktive Touren: ${data.activeTours.length}`;
    document.getElementById('log-list').innerHTML = data.logs.map(log => `<div class="log-entry">${log}</div>`).join('');

    const fleetKey = JSON.stringify(data.trucks);
    if (fleetKey !== lastFleetKey) {
      lastFleetKey = fleetKey;
      document.getElementById('truck-list').innerHTML = data.trucks.map(t => {
        const idle = t.status === 'Idle';
        const impounded = t.status === 'Impounded';
        return `
          <div class="job-card static">
            <div style="font-weight: bold;">${t.licensePlate} (${t.modelName})</div>
            <div>${t.type} · ${t.maxPayloadTons} t · steht in ${t.currentCity}</div>
            <div>Tank: ${Math.round(t.currentFuelLiters)} / ${t.fuelCapacityLiters} L</div>
            <div>Reifen ${t.tireCondition.toFixed(0)}% · Motor ${t.engineCondition.toFixed(0)}% · Kabine ${t.cabinCleanliness.toFixed(0)}%</div>
            <div>Letzte Wartung: ${t.lastMaintenance}</div>
            <div style="color: ${idle ? '#69f0ae' : '#ffb74d'}; font-weight: bold;">Status: ${t.status}</div>
            <div class="card-actions">
              ${idle ? `<button class="btn-tiny" data-action="refuel" data-id="${t.id}">Tanken</button>
                <button class="btn-tiny" data-action="maintain" data-level="PatchJob" data-id="${t.id}">TÜV-Notdurft</button>
                <button class="btn-tiny" data-action="maintain" data-level="Standard" data-id="${t.id}">Inspektion</button>
                <button class="btn-tiny" data-action="maintain" data-level="Premium" data-id="${t.id}">Premium</button>` : ''}
              ${impounded ? `<button class="btn-tiny" data-action="bail" data-id="${t.id}">Kaution</button>` : ''}
            </div>
          </div>
        `;
      }).join('');
    }

    const driverKey = JSON.stringify(data.drivers);
    if (driverKey !== lastDriverKey) {
      lastDriverKey = driverKey;
      document.getElementById('driver-list').innerHTML = data.drivers.map(d => {
        const ok = d.status === 'Available';
        return `
          <div class="job-card static">
            <div style="font-weight: bold;">${d.name}</div>
            <div>Können ${d.drivingSkill} · Zuverl. ${d.reliability} · Stress ${d.stressResistance} · Loyal ${d.loyalty}</div>
            <div>Gesundheit ${d.health}% · Moral ${d.morale}%</div>
            <div>Gehalt ${d.currentSalary.toLocaleString('de-DE')} € (Markt ${d.expectedSalary.toLocaleString('de-DE')} €)</div>
            ${d.sickLeaveDaysRemaining > 0 ? `<div>Kranktage: ${d.sickLeaveDaysRemaining}</div>` : ''}
            <div style="color: ${ok ? '#69f0ae' : '#ffb74d'}; font-weight: bold;">Status: ${d.status}</div>
            <div class="card-actions">
              <button class="btn-tiny" data-action="bonus" data-id="${d.id}">Bonus</button>
              <button class="btn-tiny" data-action="salary" data-id="${d.id}" data-salary="${d.currentSalary}">Gehalt</button>
              ${d.status === 'Arrested' ? `<button class="btn-tiny" data-action="bail-driver" data-id="${d.id}">Kaution</button>` : ''}
            </div>
          </div>
        `;
      }).join('');
    }

    const seenDepots = new Set(data.depots.map(d => d.cityName));
    for (const [name, marker] of depotMarkers.entries()) {
      if (!seenDepots.has(name)) {
        map.removeLayer(marker);
        depotMarkers.delete(name);
      }
    }
    data.depots.forEach(d => {
      if (depotMarkers.has(d.cityName) || !d.location) return;
      const marker = L.marker([d.location.latitude, d.location.longitude])
        .addTo(map)
        .bindPopup(`${d.isHome ? 'Heimatdepot' : 'Depot'} ${d.cityName}`);
      depotMarkers.set(d.cityName, marker);
    });

    const activeServerIds = new Set(data.activeTours.map(t => t.id));
    for (const [id, tourObj] of mapTours.entries()) {
      if (!activeServerIds.has(id)) {
        map.removeLayer(tourObj.polyline);
        map.removeLayer(tourObj.marker);
        mapTours.delete(id);
      }
    }

    data.activeTours.forEach(tour => {
      if (!tour.currentPoint) return;
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

        mapTours.set(tour.id, { polyline, marker });
      } else {
        tourObj.marker.setLatLng([tour.currentPoint.latitude, tour.currentPoint.longitude]);
      }
    });
  } catch (err) {
    console.error('Sync error:', err);
  }
}

document.getElementById('refresh-jobs-btn').onclick = async () => {
  await postJson('/api/jobs/refresh', {});
  loadJobs();
};

loadJobs();
syncGameState();
setInterval(syncGameState, 1000);
