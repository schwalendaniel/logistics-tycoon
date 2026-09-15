<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import L from 'leaflet';

const state = ref({ balance: 0, day: 1, hour: 8, speedMultiplier: 1, trucks: [], drivers: [], depots: [], activeTours: [], logs: [] });
const jobs = ref([]);
const market = ref({ trucks: [], drivers: [], depots: [] });
const activeTab = ref('fleet');
const modalOpen = ref(false);
const selectedJob = ref(null);
const speed = ref(1);
const loading = ref(true);
const error = ref('');
const mapElement = ref(null);
let map;
let pollTimer;
const markers = new Map();

const tabs = [
  ['fleet', 'Fuhrpark'],
  ['drivers', 'Personal'],
  ['market', 'Marktplatz'],
  ['finance', 'Management']
];

const money = value => Number(value || 0).toLocaleString('de-DE', { minimumFractionDigits: 2 });
const duration = hours => hours < 1 ? `${Math.round(hours * 60)} Spielmin.` : `${hours.toLocaleString('de-DE', { maximumFractionDigits: 1 })} Spielstd.`;
const truckIcon = type => type === 'SemiTruck' ? '🚛' : type === 'Rigid' ? '🚚' : '🚐';
const initials = name => String(name).split(/\s+/).map(part => part[0]).slice(0, 2).join('').toUpperCase();
const activeTourFor = truck => state.value.activeTours.find(tour => tour.truckPlate === truck.licensePlate);
const depotFor = truck => state.value.depots.find(depot => depot.cityName.toLowerCase() === String(truck.currentCity).toLowerCase());
const availableDrivers = computed(() => state.value.drivers.filter(driver => driver.status === 'Available'));
const currentView = computed(() => activeTab.value === 'fleet' ? state.value.trucks : state.value.drivers);
const mapIcon = (kind, label) => L.divIcon({
  className: `map-marker map-marker-${kind}`,
  html: `<span>${label}</span>`,
  iconSize: [38, 38],
  iconAnchor: [19, 19],
  popupAnchor: [0, -19]
});

async function api(path, options) {
  const response = await fetch(path, options);
  if (!response.ok) throw new Error(await response.text());
  return response.status === 204 ? null : response.json();
}

async function refreshState() {
  try {
    const [nextState, nextJobs] = await Promise.all([api('/api/state'), api('/api/jobs')]);
    state.value = nextState;
    jobs.value = nextJobs;
    speed.value = nextState.speedMultiplier;
    updateMap();
  } catch (requestError) {
    error.value = requestError.message || 'Verbindung zum Spielserver fehlgeschlagen.';
  } finally {
    loading.value = false;
  }
  pollTimer = window.setTimeout(refreshState, Math.max(250, Math.round(1000 / (state.value.speedMultiplier || 1))));
}

async function openManagement(tab) {
  activeTab.value = tab;
  modalOpen.value = true;
  if (tab === 'market' || tab === 'finance') {
    market.value = await api('/api/market').catch(() => market.value);
  }
}

function closeManagement() { modalOpen.value = false; }

async function setSpeed() {
  try {
    const result = await api('/api/settings/speed', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ speedMultiplier: Number(speed.value) }) });
    speed.value = result.speedMultiplier;
  } catch (requestError) { error.value = requestError.message; }
}

async function dispatch(job) {
  selectedJob.value = job;
  try {
    const truck = state.value.trucks.find(item => item.status === 'Idle' && item.maxPayloadTons >= job.cargoWeightTons);
    const driver = availableDrivers.value[0];
    if (!truck || !driver) throw new Error('Kein passendes Fahrzeug oder kein verfügbarer Fahrer.');
    await api('/api/tours/dispatch', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ jobId: job.id, truckId: truck.id, driverId: driver.id }) });
    await refreshState();
  } catch (requestError) { error.value = requestError.message; }
}

async function action(path, body) {
  try { await api(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }); await refreshState(); }
  catch (requestError) { error.value = requestError.message; }
}

function updateMap() {
  if (!map) return;
  const activeKeys = new Set();
  state.value.depots.forEach(depot => {
    const key = `depot-${depot.cityName}`;
    activeKeys.add(key);
    const point = [depot.location.latitude, depot.location.longitude];
    const popup = `<strong>Depot ${depot.cityName}</strong><br>${depot.occupied}/${depot.capacity} Fahrzeuge`;
    const marker = markers.get(key) || L.marker(point, { icon: mapIcon('depot', '🏢') }).addTo(map);
    marker.setLatLng(point).bindPopup(popup);
    markers.set(key, marker);
  });
  state.value.trucks.forEach(truck => {
    const tour = activeTourFor(truck);
    const point = tour?.currentPoint ? [tour.currentPoint.latitude, tour.currentPoint.longitude] : [truck.location.latitude, truck.location.longitude];
    if (!point[0] || !point[1]) return;
    const key = `truck-${truck.id}`;
    activeKeys.add(key);
    const marker = markers.get(key) || L.marker(point, { icon: mapIcon('truck', truckIcon(truck.type)) }).addTo(map);
    marker.setLatLng(point).bindPopup(`<strong>${truck.licensePlate}</strong><br>${truck.modelName}<br>${truck.currentCity} · ${truck.status}`);
    markers.set(key, marker);
  });
  markers.forEach((marker, key) => { if (!activeKeys.has(key)) { marker.remove(); markers.delete(key); } });
}

onMounted(() => {
  map = L.map(mapElement.value, { center: [51.1657, 10.4515], zoom: 6, minZoom: 5, maxZoom: 14 });
  L.tileLayer('https://tile.openstreetmap.de/{z}/{x}/{y}.png', { attribution: '&copy; OpenStreetMap Deutschland' }).addTo(map);
  refreshState();
});

onBeforeUnmount(() => { clearTimeout(pollTimer); map?.remove(); });
</script>

<template>
  <main class="app-shell">
    <div ref="mapElement" class="map"></div>
    <aside class="sidebar">
      <header class="sidebar-header"><strong>Disponenten-Zentrale</strong><span class="eyebrow">LIVE</span></header>
      <section class="sidebar-content">
        <div class="balance">{{ money(state.balance) }} €</div>
        <div class="status">Tag {{ state.day }}, {{ state.hour }}:00 · {{ state.activeTours.length }} aktive Touren</div>
        <label class="speed-control"><span>Spieltempo <b>{{ Number(speed).toLocaleString('de-DE', { minimumFractionDigits: 1 }) }}x</b></span><input v-model="speed" type="range" min="0.5" max="10" step="0.5" @change="setSpeed"></label>
        <div v-if="error" class="error">{{ error }} <button @click="error = ''">Schließen</button></div>
        <h2>Verfügbare Fracht</h2>
        <div v-if="loading" class="empty">Lade Frachtbörse ...</div>
        <button v-for="job in jobs" :key="job.id" class="job" @click="dispatch(job)">
          <strong>{{ job.title }}</strong><span>{{ job.originCity }} → {{ job.destinationCity }}</span><small>{{ money(job.revenue) }} € · Frist {{ duration(job.remainingGameHours) }}</small>
        </button>
        <button class="secondary" @click="action('/api/jobs/refresh', {})">Börse auffüllen</button>
      </section>
      <section class="logs"><h2>Ereignisse</h2><p v-for="log in state.logs" :key="log">{{ log }}</p></section>
      <nav class="sidebar-nav"><button @click="openManagement('fleet')">🚚 Fuhrpark</button><button @click="openManagement('drivers')">👤 Personal</button><button @click="openManagement('market')">🏪 Marktplatz</button><button @click="openManagement('finance')">💼 Management</button></nav>
    </aside>

    <div v-if="modalOpen" class="modal-backdrop" @click.self="closeManagement">
      <section class="management-window">
        <header class="management-header"><div><h1>{{ tabs.find(tab => tab[0] === activeTab)?.[1] }}</h1><span>Live-Daten ohne vollständiges Neurendern der Seite</span></div><button class="close" @click="closeManagement">×</button></header>
        <nav class="tabs"><button v-for="tab in tabs" :key="tab[0]" :class="{ active: activeTab === tab[0] }" @click="openManagement(tab[0])">{{ tab[1] }}</button></nav>
        <div class="management-content">
          <template v-if="activeTab === 'fleet'">
            <article v-for="truck in state.trucks" :key="truck.id" class="card">
              <div class="card-hero"><span class="vehicle-icon">{{ truckIcon(truck.type) }}</span><b>{{ truck.status }}</b></div><h3>{{ truck.licensePlate }} · {{ truck.modelName }}</h3><p>{{ truck.currentCity }} · {{ truck.engineCondition.toFixed(1) }} % Motor · {{ truck.tireCondition.toFixed(1) }} % Reifen</p><div class="actions"><button :disabled="truck.status !== 'Idle'" @click="action('/api/fleet/refuel', { truckId: truck.id })">⛽ Tanken</button><button :disabled="truck.status !== 'Idle' || depotFor(truck)" @click="action('/api/fleet/return-to-depot', { truckId: truck.id })">🏢 Depot</button></div>
            </article>
          </template>
          <template v-else-if="activeTab === 'drivers'">
            <article v-for="driver in state.drivers" :key="driver.id" class="card"><div class="avatar">{{ initials(driver.name) }}</div><h3>{{ driver.name }}</h3><p>{{ driver.currentSalary.toLocaleString('de-DE') }} €/Monat · Moral {{ driver.morale }} %</p><div class="actions"><button @click="action('/api/personnel/bonus', { driverId: driver.id, amount: 250 })">Bonus 250 €</button><button @click="action('/api/personnel/salary', { driverId: driver.id, monthlySalary: driver.currentSalary + 100 })">+100 € Gehalt</button></div></article>
          </template>
          <template v-else-if="activeTab === 'market'"><article v-for="truck in market.trucks" :key="truck.catalogId" class="card"><div class="vehicle-icon">{{ truckIcon(truck.type) }}</div><h3>{{ truck.modelName }}</h3><p>{{ truck.price.toLocaleString('de-DE') }} € · {{ truck.maxPayloadTons }} t</p><button @click="action('/api/fleet/buy', { catalogId: truck.catalogId })">Kaufen</button></article></template>
          <template v-else><article class="card finance-card"><h3>Management</h3><p>Bonität und Kredite bleiben über den bestehenden Finanzbereich verfügbar.</p><button @click="action('/api/finance/borrow', { type: 'Bank', amount: 5000 })">5.000 € Bankkredit</button></article></template>
        </div>
      </section>
    </div>
  </main>
</template>
