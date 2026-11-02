<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import './style.css';

// --- KONSTANTEN & DEFINITIONEN ---
const TABS = [
  { id: 'fleet', label: 'Fuhrpark' },
  { id: 'drivers', label: 'Personal' },
  { id: 'market', label: 'Marktplatz' },
  { id: 'finance', label: 'Management' }
];

// --- REAKTIVER ZUSTAND ---
const state = ref({
  balance: 0,
  day: 1,
  hour: 8,
  speedMultiplier: 1,
  trucks: [],
  drivers: [],
  depots: [],
  activeTours: [],
  logs: []
});

const jobs = ref([]);
const market = ref({ trucks: [], drivers: [], depots: [] });
const activeTab = ref('fleet');
const modalOpen = ref(false);
const speed = ref(1);
const loading = ref(true);
const error = ref('');

// --- MAP & TIMER REFERENZEN (NICHT-REAKTIV) ---
const mapElement = ref(null);
let map = null;
let pollTimer = null;
const markers = new Map();

// --- COMPUTED PROPERTIES ---
const currentTabLabel = computed(() => {
  return TABS.find(tab => tab.id === activeTab.value)?.label ?? '';
});

const availableDrivers = computed(() => {
  return state.value.drivers.filter(driver => driver.status === 'Available');
});

// --- FORMATIERER & ICON-HELPERS ---
const money = value => Number(value || 0).toLocaleString('de-DE', { minimumFractionDigits: 2 });

const duration = hours => {
  if (hours < 1) return `${Math.round(hours * 60)} Spielmin.`;
  return `${hours.toLocaleString('de-DE', { maximumFractionDigits: 1 })} Spielstd.`;
};

const truckIcon = type => (type === 'SemiTruck' ? '🚛' : type === 'Rigid' ? '🚚' : '🚐');

const initials = name => {
  return String(name)
    .split(/\s+/)
    .map(part => part[0])
    .slice(0, 2)
    .join('')
    .toUpperCase();
};

const activeTourFor = truck => {
  return state.value.activeTours.find(tour => tour.truckPlate === truck.licensePlate);
};

const depotFor = truck => {
  return state.value.depots.find(
    depot => depot.cityName.toLowerCase() === String(truck.currentCity).toLowerCase()
  );
};

const createMapIcon = (kind, label) => {
  return L.divIcon({
    className: `map-marker map-marker-${kind}`,
    html: `<span>${label}</span>`,
    iconSize: [38, 38],
    iconAnchor: [19, 19],
    popupAnchor: [0, -19]
  });
};

// --- HTTP API ---
async function api(path, options = {}) {
  const response = await fetch(path, options);
  if (!response.ok) {
    const errorMsg = await response.text();
    throw new Error(errorMsg);
  }
  return response.status === 204 ? null : response.json();
}

async function sendAction(path, body = {}) {
  try {
    await api(path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    });
    await refreshState();
  } catch (err) {
    error.value = err.message;
  }
}

// --- SPIEL-LOGIK & SYNC ---
async function refreshState() {
  try {
    const [nextState, nextJobs] = await Promise.all([
      api('/api/state'),
      api('/api/jobs')
    ]);
    state.value = nextState;
    jobs.value = nextJobs;
    speed.value = nextState.speedMultiplier;
    updateMap();
  } catch (err) {
    error.value = err.message || 'Verbindung zum Spielserver fehlgeschlagen.';
  } finally {
    loading.value = false;
  }

  const pollInterval = Math.max(250, Math.round(1000 / (state.value.speedMultiplier || 1)));
  pollTimer = window.setTimeout(refreshState, pollInterval);
}

async function setSpeed() {
  try {
    const result = await api('/api/settings/speed', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ speedMultiplier: Number(speed.value) })
    });
    speed.value = result.speedMultiplier;
  } catch (err) {
    error.value = err.message;
  }
}

async function dispatchJob(job) {
  try {
    const truck = state.value.trucks.find(
      t => t.status === 'Idle' && t.maxPayloadTons >= job.cargoWeightTons
    );
    const driver = availableDrivers.value[0];

    if (!truck || !driver) {
      throw new Error('Kein passendes Fahrzeug oder kein verfügbarer Fahrer frei.');
    }

    await api('/api/tours/dispatch', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ jobId: job.id, truckId: truck.id, driverId: driver.id })
    });
    await refreshState();
  } catch (err) {
    error.value = err.message;
  }
}

async function openManagement(tabId) {
  activeTab.value = tabId;
  modalOpen.value = true;
  if (tabId === 'market' || tabId === 'finance') {
    market.value = await api('/api/market').catch(() => market.value);
  }
}

function closeManagement() {
  modalOpen.value = false;
}

// --- MAP RENDERING ---
function updateMap() {
  if (!map) return;
  const activeKeys = new Set();

  // 1. Depots synchronisieren
  state.value.depots.forEach(depot => {
    const key = `depot-${depot.cityName}`;
    activeKeys.add(key);
    const coords = [depot.location.latitude, depot.location.longitude];
    const popup = `<strong>Depot ${depot.cityName}</strong><br>${depot.occupied}/${depot.capacity} Fahrzeuge`;

    const marker = markers.get(key) || L.marker(coords, { icon: createMapIcon('depot', '🏢') }).addTo(map);
    marker.setLatLng(coords).bindPopup(popup);
    markers.set(key, marker);
  });

  // 2. Trucks synchronisieren
  state.value.trucks.forEach(truck => {
    const tour = activeTourFor(truck);
    const coords = tour?.currentPoint
      ? [tour.currentPoint.latitude, tour.currentPoint.longitude]
      : [truck.location.latitude, truck.location.longitude];

    if (!coords[0] || !coords[1]) return;

    const key = `truck-${truck.id}`;
    activeKeys.add(key);
    const popup = `<strong>${truck.licensePlate}</strong><br>${truck.modelName}<br>${truck.currentCity} · ${truck.status}`;

    const marker = markers.get(key) || L.marker(coords, { icon: createMapIcon('truck', truckIcon(truck.type)) }).addTo(map);
    marker.setLatLng(coords).bindPopup(popup);
    markers.set(key, marker);
  });

  // 3. Veraltete Marker entfernen
  markers.forEach((marker, key) => {
    if (!activeKeys.has(key)) {
      marker.remove();
      markers.delete(key);
    }
  });
}

// --- LIFECYCLE HOOKS ---
onMounted(() => {
  map = L.map(mapElement.value, { center: [51.1657, 10.4515], zoom: 6, minZoom: 5, maxZoom: 14 });
  L.tileLayer('https://tile.openstreetmap.de/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap Deutschland'
  }).addTo(map);

  refreshState();
});

onBeforeUnmount(() => {
  clearTimeout(pollTimer);
  map?.remove();
});
</script>

<template>
  <main class="app-shell">
    <!-- Interaktive Karte -->
    <div ref="mapElement" class="map"></div>

    <!-- Live-Disponenten-Sidebar -->
    <aside class="sidebar">
      <header class="sidebar-header">
        <strong>Disponenten-Zentrale</strong>
        <span class="eyebrow">LIVE</span>
      </header>

      <section class="sidebar-content">
        <div class="balance">{{ money(state.balance) }} €</div>
        <div class="status">
          Tag {{ state.day }}, {{ state.hour }}:00 · {{ state.activeTours.length }} aktive Touren
        </div>

        <label class="speed-control">
          <span>Spieltempo <b>{{ Number(speed).toLocaleString('de-DE', { minimumFractionDigits: 1 }) }}x</b></span>
          <input
            v-model="speed"
            type="range"
            min="0.5"
            max="10"
            step="0.5"
            @change="setSpeed"
          />
        </label>

        <div v-if="error" class="error">
          {{ error }}
          <button @click="error = ''">Schließen</button>
        </div>

        <h2>Verfügbare Fracht</h2>
        <div v-if="loading" class="empty">Lade Frachtbörse ...</div>

        <button
          v-for="job in jobs"
          :key="job.id"
          class="job"
          @click="dispatchJob(job)"
        >
          <strong>{{ job.title }}</strong>
          <span>{{ job.originCity }} → {{ job.destinationCity }}</span>
          <small>{{ money(job.revenue) }} € · Frist {{ duration(job.remainingGameHours) }}</small>
        </button>

        <button class="secondary" @click="sendAction('/api/jobs/refresh')">
          Börse auffüllen
        </button>
      </section>

      <section class="logs">
        <h2>Ereignisse</h2>
        <p v-for="log in state.logs" :key="log">{{ log }}</p>
      </section>

      <nav class="sidebar-nav">
        <button
          v-for="tab in TABS"
          :key="tab.id"
          @click="openManagement(tab.id)"
        >
          {{ tab.label }}
        </button>
      </nav>
    </aside>

    <!-- Modal-Fenster für Management -->
    <div v-if="modalOpen" class="modal-backdrop" @click.self="closeManagement">
      <section class="management-window">
        <header class="management-header">
          <div>
            <h1>{{ currentTabLabel }}</h1>
            <span>Live-Flottenverwaltung</span>
          </div>
          <button class="close" @click="closeManagement">×</button>
        </header>

        <nav class="tabs">
          <button
            v-for="tab in TABS"
            :key="tab.id"
            :class="{ active: activeTab === tab.id }"
            @click="openManagement(tab.id)"
          >
            {{ tab.label }}
          </button>
        </nav>

        <div class="management-content">
          <!-- Fuhrpark Tab -->
          <template v-if="activeTab === 'fleet'">
            <article v-for="truck in state.trucks" :key="truck.id" class="card">
              <div class="card-hero">
                <span class="vehicle-icon">{{ truckIcon(truck.type) }}</span>
                <b>{{ truck.status }}</b>
              </div>
              <h3>{{ truck.licensePlate }} · {{ truck.modelName }}</h3>
              <p>
                {{ truck.currentCity }} · Motor: {{ truck.engineCondition.toFixed(1) }}% · Reifen: {{ truck.tireCondition.toFixed(1) }}%
              </p>
              <div class="actions">
                <button
                  :disabled="truck.status !== 'Idle'"
                  @click="sendAction('/api/fleet/refuel', { truckId: truck.id })"
                >
                  ⛽ Tanken
                </button>
                <button
                  :disabled="truck.status !== 'Idle' || depotFor(truck)"
                  @click="sendAction('/api/fleet/return-to-depot', { truckId: truck.id })"
                >
                  🏢 Depot
                </button>
              </div>
            </article>
          </template>

          <!-- Personal Tab -->
          <template v-else-if="activeTab === 'drivers'">
            <article v-for="driver in state.drivers" :key="driver.id" class="card">
              <div class="avatar">{{ initials(driver.name) }}</div>
              <h3>{{ driver.name }}</h3>
              <p>{{ driver.currentSalary.toLocaleString('de-DE') }} €/Monat · Moral {{ driver.morale }}%</p>
              <div class="actions">
                <button @click="sendAction('/api/personnel/bonus', { driverId: driver.id, amount: 250 })">
                  Bonus 250 €
                </button>
                <button @click="sendAction('/api/personnel/salary', { driverId: driver.id, monthlySalary: driver.currentSalary + 100 })">
                  +100 € Gehalt
                </button>
              </div>
            </article>
          </template>

          <!-- Marktplatz Tab -->
          <template v-else-if="activeTab === 'market'">
            <article v-for="truck in market.trucks" :key="truck.catalogId" class="card">
              <div class="vehicle-icon">{{ truckIcon(truck.type) }}</div>
              <h3>{{ truck.modelName }}</h3>
              <p>{{ truck.price.toLocaleString('de-DE') }} € · Nutzlast: {{ truck.maxPayloadTons }} t</p>
              <button @click="sendAction('/api/fleet/buy', { catalogId: truck.catalogId })">
                Kaufen
              </button>
            </article>
          </template>

          <!-- Finanzen Tab -->
          <template v-else>
            <article class="card finance-card">
              <h3>Finanzmanagement</h3>
              <p>Liquidität und Darlehensverwaltung für den Fuhrparkausbau.</p>
              <button @click="sendAction('/api/finance/borrow', { type: 'Bank', amount: 5000 })">
                5.000 € Bankkredit aufnehmen
              </button>
            </article>
          </template>
        </div>
      </section>
    </div>
  </main>
</template>