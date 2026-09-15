import { t } from './i18n.js';

const setElementText = (id, text) => {
  const el = document.getElementById(id);
  if (el) el.innerText = text;
};

setElementText('panel-title', t('panelTitle'));
setElementText('status-box', t('readyToDispatch'));
setElementText('refresh-jobs-btn', t('refreshJobs'));

// Die Verwaltung bleibt auch nutzbar, wenn Leaflet oder die Kartenkacheln nicht laden.
let map = null;
if (typeof L !== 'undefined') {
  const germanyBounds = L.latLngBounds(L.latLng(46.5, 4.5), L.latLng(55.8, 16.0));
  map = L.map('map', {
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
}

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
const tabManagementBtn = document.getElementById('mgmt-tab-management-btn');
const gameSpeedSlider = document.getElementById('game-speed-slider');
const gameSpeedValue = document.getElementById('game-speed-value');

const viewFleet = document.getElementById('mgmt-fleet-view');
const viewDriver = document.getElementById('mgmt-driver-view');
const viewMarketTrucks = document.getElementById('mgmt-market-trucks-view');
const viewMarketDrivers = document.getElementById('mgmt-market-drivers-view');
const viewManagement = document.getElementById('mgmt-management-view');
let activeManagementTab = 'fleet';

document.getElementById('open-fleet-hub-btn').onclick = () => openManagement('fleet');
document.getElementById('open-drivers-hub-btn').onclick = () => openManagement('drivers');
document.getElementById('open-market-hub-btn').onclick = () => openManagement('market-trucks');
tabManagementBtn.onclick = () => switchMgmtTab('management');

function openManagement(tab) {
  mgmtModal.style.display = 'flex';
  switchMgmtTab(tab);
}

closeMgmtBtn.onclick = () => { mgmtModal.style.display = 'none'; };

function switchMgmtTab(tab) {
  activeManagementTab = tab;
  const allBtns = [tabFleetBtn, tabDriverBtn, tabMarketTrucksBtn, tabMarketDriversBtn, tabManagementBtn];
  const allViews = [viewFleet, viewDriver, viewMarketTrucks, viewMarketDrivers, viewManagement];

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
  } else if (tab === 'management') {
    tabManagementBtn.classList.add('active');
    viewManagement.style.display = 'block';
    document.getElementById('mgmt-title').innerText = "Management";
    document.getElementById('mgmt-subtitle').innerText = "Kredite, Zinsen und Unternehmenssicherheit";
    loadFinance();
  }
}

tabFleetBtn.onclick = () => switchMgmtTab('fleet');
tabDriverBtn.onclick = () => switchMgmtTab('drivers');
tabMarketTrucksBtn.onclick = () => switchMgmtTab('market-trucks');
tabMarketDriversBtn.onclick = () => switchMgmtTab('market-drivers');

let speedUpdateTimer = null;
let pendingSpeedMultiplier = null;
gameSpeedSlider.oninput = () => {
  pendingSpeedMultiplier = Number(gameSpeedSlider.value);
  gameSpeedValue.value = `${Number(gameSpeedSlider.value).toLocaleString('de-DE', { minimumFractionDigits: 1, maximumFractionDigits: 1 })}x`;
  clearTimeout(speedUpdateTimer);
  speedUpdateTimer = setTimeout(updateGameSpeed, 120);
};

async function updateGameSpeed() {
  const requestedSpeed = pendingSpeedMultiplier;
  if (requestedSpeed === null) return;

  try {
    const res = await fetch('/api/settings/speed', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ speedMultiplier: requestedSpeed })
    });

    if (!res.ok) {
      alert(await res.text());
      if (pendingSpeedMultiplier === requestedSpeed) pendingSpeedMultiplier = null;
      syncGameState();
      return;
    }

    if (pendingSpeedMultiplier === requestedSpeed) pendingSpeedMultiplier = null;
  } catch (error) {
    if (pendingSpeedMultiplier === requestedSpeed) pendingSpeedMultiplier = null;
    console.error('Spieltempo konnte nicht gespeichert werden:', error);
  }
}

let cachedState = null;
let selectedJobForDispatch = null;
const mapTours = new Map();
const mapDepotMarkers = new Map();
const mapVehicleMarkers = new Map();
let statePollTimer = null;

function getTruckIllustration(type) {
  const icon = type === "SemiTruck" ? "🚛" : type === "Rigid" ? "🚚" : "🚐";
  return `<span class="vehicle-icon" role="img" aria-label="${type}">${icon}</span>`;
}

function getDriverAvatar(name) {
  const initials = name
    .split(/\s+/)
    .filter(Boolean)
    .map(part => part[0])
    .slice(0, 2)
    .join('')
    .toUpperCase();
  return `<span class="avatar-initials">${initials}</span>`;
}

function formatDuration(minutes) {
  if (minutes < 60) return `${minutes} Spielminuten`;
  const hours = minutes / 60;
  return `${hours.toLocaleString('de-DE', { maximumFractionDigits: 1 })} Spielstunden`;
}

function isViewHovered(view) {
  return view.matches(':hover');
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
        <span style="color: ${job.remainingGameHours <= 6 ? '#ff8a65' : '#94a3b8'};">Frist: ${formatDeadline(job.remainingGameHours)}</span>
      </div>
    `;
    card.onclick = () => openDispatchModal(job);
    container.appendChild(card);
  });
}

// 2. Dispatch Modal
function formatDeadline(hours) {
  if (hours <= 0) return 'abgelaufen';
  if (hours < 1) return `${Math.round(hours * 60)} Spielminuten`;
  return `${hours.toLocaleString('de-DE', { maximumFractionDigits: 1 })} Spielstunden`;
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
            ${truckImg}
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
    }).join('') + market.depots.map(depot => `
      <div class="mgmt-card depot-market-card">
        <div class="mgmt-card-hero depot-hero">
          <span class="depot-icon">🏢</span>
          <span class="mgmt-badge" style="background:#16a34a; color:#fff;">Depot</span>
        </div>
        <div class="mgmt-card-body">
          <div class="mgmt-card-title">Depot ${depot.cityName}</div>
          <div class="mgmt-card-sub">Standort auf der Karte · Kapazität: ${depot.capacity} Fahrzeuge</div>
          <div style="font-size: 18px; font-weight: bold; color: #ffd166; margin: 4px 0;">${depot.price.toLocaleString('de-DE')} €</div>
          <div class="mgmt-actions">
            <button class="btn-primary" style="margin: 0; width: 100%;" onclick="buyDepot('${depot.cityName}')">Depot kaufen</button>
          </div>
        </div>
      </div>`).join('');

    // Fahrer-Bewerberpool rendern
    viewMarketDrivers.innerHTML = market.drivers.map(d => {
      return `
        <div class="mgmt-card">
          <div class="mgmt-card-hero">
            <div class="avatar-circle" role="img" aria-label="${d.name}">${getDriverAvatar(d.name)}</div>
            <span class="mgmt-badge badge-idle">Bewerber</span>
          </div>
          <div class="mgmt-card-body">
            <div>
              <div class="mgmt-card-title">${d.name}</div>
              <div class="mgmt-card-sub">Gehaltsanspruch: ${d.askingSalary.toLocaleString('de-DE')} €/Monat</div>
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

async function loadFinance() {
  const res = await fetch('/api/finance');
  if (!res.ok) return;
  const finance = await res.json();
  const formatMoney = value => Number(value).toLocaleString('de-DE', { minimumFractionDigits: 2 });
  const offerCard = (offer, type, id, extraClass = '') => `
    <div class="finance-card ${extraClass}">
      <div class="mgmt-card-title">${offer.lender}</div>
      <div class="mgmt-card-sub">${offer.reason}</div>
      <div class="finance-rate">${Number(offer.monthlyRate).toLocaleString('de-DE', { minimumFractionDigits: 1 })}% Zinsen / Monat</div>
      <div class="finance-limit">Limit: ${formatMoney(offer.maxAmount)} €</div>
      <div class="finance-borrow-row">
        <input id="${id}-amount" class="custom-input" type="number" min="100" max="${offer.maxAmount}" step="100" value="${Math.min(offer.maxAmount, 5000)}" ${offer.maxAmount <= 0 ? 'disabled' : ''}>
        <button class="mgmt-action-btn" onclick="borrowLoan('${type}', '${id}-amount')" ${offer.maxAmount <= 0 ? 'disabled' : ''}>Aufnehmen</button>
      </div>
    </div>`;

  viewManagement.innerHTML = `
    <div class="finance-summary">
      <div class="finance-score">Bonität <strong>${finance.creditScore}</strong><span>/ 850</span></div>
      ${offerCard(finance.bank, 'Bank', 'bank-loan')}
      ${offerCard(finance.loanShark, 'LoanShark', 'shark-loan', 'finance-card-danger')}
    </div>
    <div class="finance-section-title">Offene Kredite</div>
    <div class="finance-loans">
      ${finance.loans.length === 0 ? '<div class="mgmt-card-sub">Keine offenen Kredite.</div>' : finance.loans.map(loan => `
        <div class="finance-loan-row">
          <div><strong>${loan.lender}</strong><span>${formatMoney(loan.remainingPrincipal)} € offen · ${formatMoney(loan.monthlyInterest)} € Zinsen/Monat</span></div>
          <button class="mgmt-action-btn" onclick="repayLoan('${loan.id}', ${loan.remainingPrincipal})">Tilgen</button>
        </div>`).join('')}
    </div>
    <div class="finance-bankruptcy">
      <div><strong>Insolvenz anmelden</strong><span>Setzt jederzeit einen neuen Spielstand auf und löscht alle Kredite.</span></div>
      <button class="mgmt-action-btn danger-action" onclick="declareBankruptcy()">Insolvenz</button>
    </div>`;
}

window.borrowLoan = async (type, inputId) => {
  const amount = Number(document.getElementById(inputId).value);
  const res = await fetch('/api/finance/borrow', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ type, amount })
  });
  if (!res.ok) { alert(await res.text()); return; }
  loadFinance();
  syncGameState();
};

window.repayLoan = async (loanId, remaining) => {
  const amount = Number(prompt(`Rückzahlung in Euro (max. ${remaining.toLocaleString('de-DE')}):`, remaining));
  if (!Number.isFinite(amount) || amount <= 0) return;
  const res = await fetch('/api/finance/repay', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ loanId, amount })
  });
  if (!res.ok) { alert(await res.text()); return; }
  loadFinance();
  syncGameState();
};

window.declareBankruptcy = async () => {
  if (!confirm('Insolvenz anmelden und einen neuen Spielstand beginnen? Dieser Vorgang ist endgültig.')) return;
  const res = await fetch('/api/finance/bankruptcy', { method: 'POST' });
  if (!res.ok) { alert(await res.text()); return; }
  loadFinance();
  syncGameState();
};

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

window.buyDepot = async (cityName) => {
  const res = await fetch('/api/depots/buy', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ cityName })
  });
  if (!res.ok) { alert(await res.text()); return; }
  loadMarket();
  syncGameState();
};

window.bailTruck = async (truckId) => {
  const res = await fetch('/api/fleet/bail', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ truckId })
  });
  if (!res.ok) { alert(await res.text()); return; }
  syncGameState();
};

window.sellTruck = async (truckId, resaleValue) => {
  if (!confirm(`Fahrzeug für ${resaleValue.toLocaleString('de-DE')} € verkaufen?`)) return;

  const res = await fetch('/api/fleet/sell', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ truckId })
  });
  if (!res.ok) { alert(await res.text()); return; }
  syncGameState();
};

window.recoverCargo = async (tourId, brokenDownTruckId, rescueTruckId, rescueDriverId) => {
  if (!brokenDownTruckId || !rescueTruckId || !rescueDriverId) {
    alert('Bitte Bergungsfahrzeug und Fahrer auswählen.');
    return;
  }
  const res = await fetch('/api/tours/recover', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ tourId: tourId || null, brokenDownTruckId, rescueTruckId, rescueDriverId })
  });
  if (!res.ok) { alert(await res.text()); return; }
  syncGameState();
};

window.towTruck = async (tourId, brokenDownTruckId) => {
  if (!confirm('LKW abschleppen lassen? Die Ladung geht dabei verloren.')) return;
  const res = await fetch('/api/tours/tow', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ tourId: tourId || null, brokenDownTruckId })
  });
  if (!res.ok) { alert(await res.text()); return; }
  syncGameState();
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

window.adjustDriverSalary = async (driverId, inputId) => {
  const monthlySalary = Number(document.getElementById(inputId).value);
  const res = await fetch('/api/personnel/salary', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ driverId, monthlySalary })
  });
  if (!res.ok) { alert(await res.text()); return; }
  syncGameState();
};

window.payDriverBonus = async (driverId, inputId) => {
  const amount = Number(document.getElementById(inputId).value);
  const res = await fetch('/api/personnel/bonus', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ driverId, amount })
  });
  if (!res.ok) { alert(await res.text()); return; }
  syncGameState();
};

window.dismissDriver = async (driverId, name) => {
  if (!confirm(`${name} wirklich entlassen? Es wird eine Abfindung von 50 % eines Monatsgehalts fällig.`)) return;
  const res = await fetch('/api/personnel/dismiss', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ driverId })
  });
  if (!res.ok) { alert(await res.text()); return; }
  syncGameState();
};

// Wartungs-Funktion mit Enum-String & sauberer Fehlerbehandlung
window.maintainTruck = async (truckId, level) => {
  try {
    const res = await fetch('/api/fleet/maintain', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ 
        truckId: truckId, 
        level: level
      })
    });

    if (!res.ok) {
      const err = await res.text();
      alert(`Wartung abgelehnt: ${err}`);
      return;
    }
    syncGameState();
  } catch (e) {
    alert("Netzwerkfehler bei der Werkstattanfrage.");
  }
};

window.refuelTruck = async (truckId) => {
  try {
    const res = await fetch('/api/fleet/refuel', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ truckId: truckId })
    });

    if (!res.ok) {
      const err = await res.text();
      alert(`Tanken nicht möglich: ${err}`);
      return;
    }
    syncGameState();
  } catch (e) {
    alert("Netzwerkfehler beim Tanken.");
  }
};

window.returnTruckToDepot = async (truckId) => {
  const res = await fetch('/api/fleet/return-to-depot', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ truckId })
  });
  if (!res.ok) { alert(await res.text()); return; }
  syncGameState();
};

// 4. Live GameState Synchronisation
async function syncGameState() {
  try {
    const res = await fetch('/api/state');
    if (!res.ok) return;

    const data = await res.json();
    cachedState = data;
    if (pendingSpeedMultiplier === null) {
      gameSpeedSlider.value = String(data.speedMultiplier);
      gameSpeedValue.value = `${Number(data.speedMultiplier).toLocaleString('de-DE', { minimumFractionDigits: 1, maximumFractionDigits: 1 })}x`;
    }

    document.getElementById('balance-box').innerText = `Kontostand: ${data.balance.toLocaleString('de-DE', { minimumFractionDigits: 2 })} €`;
    document.getElementById('status-box').innerText = `Aktive Touren: ${data.activeTours.length} | Tag ${data.day}, ${data.hour}:00 Uhr`;
    document.getElementById('log-list').innerHTML = data.logs.map(log => `<div class="log-entry">${log}</div>`).join('');

    // Nur die sichtbare Management-Ansicht neu rendern.
    if (activeManagementTab === 'fleet' && !isViewHovered(viewFleet)) {
      viewFleet.innerHTML = data.trucks.map(t => {
      const isIdle = t.status === "Idle";
      const isMaintenance = t.status === "Maintenance";
      const isImpounded = t.status === "Impounded";
      const isBrokenDown = t.status === "BrokenDown";
      const fuelPct = Math.min(100, (t.currentFuelLiters / t.fuelCapacityLiters) * 100);
      const badgeClass = isIdle ? 'badge-idle' : isMaintenance || isImpounded || isBrokenDown ? 'badge-danger' : 'badge-active';
      const truckImg = getTruckIllustration(t.type);
      const isInDepot = data.depots.some(d => d.cityName === t.currentCity);
      const activeTour = data.activeTours.find(tour => tour.truckPlate === t.licensePlate);
      const activeTourId = activeTour?.id ?? t.activeTourId;
      const rescueOptions = data.trucks
        .filter(rescueTruck => rescueTruck.id !== t.id && rescueTruck.status === 'Idle' && rescueTruck.maxPayloadTons >= (activeTour?.cargoWeightTons ?? 0))
        .map(rescueTruck => `<option value="${rescueTruck.id}">${rescueTruck.licensePlate} · ${rescueTruck.modelName}</option>`)
        .join('');
      const rescueDriverOptions = data.drivers
        .filter(driver => driver.status === 'Available')
        .map(driver => `<option value="${driver.id}">${driver.name}</option>`)
        .join('');

      return `
        <div class="mgmt-card">
          <div class="mgmt-card-hero">
            ${truckImg}
            <span class="mgmt-badge ${badgeClass}">${t.status}</span>
          </div>
          <div class="mgmt-card-body">
            <div>
              <div class="mgmt-card-title">${t.licensePlate} · ${t.modelName}</div>
              <div class="mgmt-card-sub">${t.type} · Zuladung: ${t.maxPayloadTons}t · Ort: <b>${t.currentCity || 'Unterwegs'}</b> ${isInDepot ? '🏢 (Depot)' : ''}</div>
              <div class="mgmt-card-sub">Restwert: <b>${t.resaleValue.toLocaleString('de-DE')} €</b></div>
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

            ${isImpounded ? `
              <div class="mgmt-actions" style="margin-top: auto;">
                <button class="mgmt-action-btn" onclick="bailTruck('${t.id}')">
                  🔓 Auslösen (2.800 €)
                </button>
              </div>
            ` : isBrokenDown ? `
              <div class="breakdown-actions">
                <div class="breakdown-note">Panne: LKW steht. Bergung oder Abschleppen nötig.</div>
                <select id="rescue-${t.id}" class="custom-select" ${rescueOptions ? '' : 'disabled'}>
                  <option value="">Bergungs-LKW wählen</option>
                  ${rescueOptions}
                </select>
                <select id="rescue-driver-${t.id}" class="custom-select" ${rescueDriverOptions ? '' : 'disabled'}>
                  <option value="">Bergungsfahrer wählen</option>
                  ${rescueDriverOptions}
                </select>
                <button class="mgmt-action-btn" onclick="recoverCargo(${JSON.stringify(activeTourId ?? null)}, '${t.id}', document.getElementById('rescue-${t.id}').value, document.getElementById('rescue-driver-${t.id}').value)" ${rescueOptions && rescueDriverOptions ? '' : 'disabled'}>
                  📦 Ladung bergen
                </button>
                <button class="mgmt-action-btn" onclick="towTruck(${JSON.stringify(activeTourId ?? null)}, '${t.id}')">🚨 Abschleppen</button>
              </div>
            ` : isMaintenance ? `
              <div style="font-size: 11px; color: #f59e0b; padding: 8px; background: rgba(245, 158, 11, 0.1); border-radius: 6px; text-align: center; margin-top: auto;">
                🔧 In Inspektion (${formatDuration(t.maintenanceMinutesRemaining)} verbleibend)
              </div>
            ` : `
              <div class="mgmt-actions" style="flex-wrap: wrap;">
                <button class="mgmt-action-btn" onclick="refuelTruck('${t.id}')" ${!isIdle ? 'disabled' : ''} style="flex: 1 1 100%;">
                  ⛽ Volltanken
                </button>
                <button class="mgmt-action-btn" onclick="returnTruckToDepot('${t.id}')" ${!isIdle || isInDepot ? 'disabled' : ''}>
                  🏢 Zum Depot
                </button>
                ${isInDepot ? `<button class="mgmt-action-btn" onclick="maintainTruck('${t.id}', 'PatchJob')" ${!isIdle ? 'disabled' : ''} title="Patch-Job">
                  🔧 Patch-Job
                </button>
                <button class="mgmt-action-btn" onclick="maintainTruck('${t.id}', 'Standard')" ${!isIdle ? 'disabled' : ''} title="Standard-Inspektion">
                  🔧 Standard
                </button>
                <button class="mgmt-action-btn" onclick="maintainTruck('${t.id}', 'Premium')" ${!isIdle ? 'disabled' : ''} title="Premium-Service">
                  🔧 Premium
                </button>` : ''}
                <button class="mgmt-action-btn" onclick="sellTruck('${t.id}', ${t.resaleValue})" ${!isIdle ? 'disabled' : ''} title="Restwert abhängig von Zustand und Kilometern">
                  💰 Verkaufen
                </button>
              </div>
            `}
          </div>
        </div>
      `;
      }).join('');
    }

    // Eigenes Personal rendern
    if (activeManagementTab === 'drivers' && !isViewHovered(viewDriver)) {
      viewDriver.innerHTML = data.drivers.map(d => {
      const isAvail = d.status === "Available";
      const isSick = d.status === "SickLeave";
      const badgeClass = isAvail ? 'badge-idle' : isSick ? 'badge-danger' : 'badge-active';

      return `
        <div class="mgmt-card">
          <div class="mgmt-card-hero">
            <div class="avatar-circle" role="img" aria-label="${d.name}">${getDriverAvatar(d.name)}</div>
            <span class="mgmt-badge ${badgeClass}">${d.status}</span>
          </div>
          <div class="mgmt-card-body">
            <div>
              <div class="mgmt-card-title">${d.name}</div>
              <div class="mgmt-card-sub">Lohn: ${d.currentSalary.toLocaleString('de-DE')} €/Monat · Soll: ${d.expectedSalary.toLocaleString('de-DE')} €</div>
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
            <div class="personnel-controls">
              <div class="personnel-control-row">
                <input id="salary-${d.id}" class="custom-input" type="number" min="1500" max="8000" step="50" value="${d.currentSalary}">
                <button class="mgmt-action-btn" onclick="adjustDriverSalary('${d.id}', 'salary-${d.id}')">Gehalt setzen</button>
              </div>
              <div class="personnel-control-row">
                <input id="bonus-${d.id}" class="custom-input" type="number" min="50" max="5000" step="50" value="250">
                <button class="mgmt-action-btn" onclick="payDriverBonus('${d.id}', 'bonus-${d.id}')">Bonus zahlen</button>
              </div>
              <button class="mgmt-action-btn danger-action" onclick="dismissDriver('${d.id}', '${d.name.replace(/'/g, "\\'")}')" ${d.status === 'OnRoute' ? 'disabled' : ''}>Fahrer entlassen</button>
            </div>
          </div>
        </div>
      `;
      }).join('');
    }

    if (!map) return;

    const activeTourByTruck = new Map(data.activeTours.map(tour => [tour.truckPlate, tour]));
    const activeDepotIds = new Set();
    data.depots.forEach(depot => {
      activeDepotIds.add(depot.cityName);
      const position = [depot.location.latitude, depot.location.longitude];
      let marker = mapDepotMarkers.get(depot.cityName);
      if (!marker) {
        marker = L.marker(position, { title: `Depot ${depot.cityName}` })
          .addTo(map)
          .bindPopup(`<strong>Depot ${depot.cityName}</strong><br>Fahrzeuge: ${depot.occupied}/${depot.capacity}`);
        mapDepotMarkers.set(depot.cityName, marker);
      } else {
        marker.setLatLng(position).setPopupContent(`<strong>Depot ${depot.cityName}</strong><br>Fahrzeuge: ${depot.occupied}/${depot.capacity}`);
      }
    });
    for (const [cityName, marker] of mapDepotMarkers.entries()) {
      if (!activeDepotIds.has(cityName)) {
        map.removeLayer(marker);
        mapDepotMarkers.delete(cityName);
      }
    }

    const activeTruckIds = new Set();
    data.trucks.forEach(truck => {
      activeTruckIds.add(truck.id);
      const tour = activeTourByTruck.get(truck.licensePlate);
      const position = tour?.currentPoint
        ? [tour.currentPoint.latitude, tour.currentPoint.longitude]
        : [truck.location.latitude, truck.location.longitude];
      if (!position[0] || !position[1]) return;

      let marker = mapVehicleMarkers.get(truck.id);
      const popup = `<strong>${truck.licensePlate}</strong><br>${truck.modelName}<br>${truck.currentCity} · ${truck.status}`;
      if (!marker) {
        marker = L.marker(position, { title: truck.licensePlate }).addTo(map).bindPopup(popup);
        mapVehicleMarkers.set(truck.id, marker);
      } else {
        marker.setLatLng(position).setPopupContent(popup);
      }
    });
    for (const [truckId, marker] of mapVehicleMarkers.entries()) {
      if (!activeTruckIds.has(truckId)) {
        map.removeLayer(marker);
        mapVehicleMarkers.delete(truckId);
      }
    }

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

function statePollInterval(speedMultiplier) {
  // Bei hohem Tempo häufiger abfragen, damit kein sichtbarer Stunden-Schritt übersprungen wird.
  return Math.max(100, Math.min(1000, Math.round(1000 / speedMultiplier)));
}

function startStatePolling() {
  const poll = async () => {
    await syncGameState();
    const speed = cachedState?.speedMultiplier ?? 1;
    statePollTimer = setTimeout(poll, statePollInterval(speed));
  };

  clearTimeout(statePollTimer);
  poll();
}

document.getElementById('refresh-jobs-btn').onclick = async () => {
  await fetch('/api/jobs/refresh', { method: 'POST' });
  loadJobs();
};

loadJobs();
startStatePolling();