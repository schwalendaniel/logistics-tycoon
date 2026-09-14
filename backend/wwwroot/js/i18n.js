const translations = {
  de: {
    appTitle: "Logistics Tycoon - Disponenten-Zentrale",
    panelTitle: "Disponenten-Zentrale",
    companyBalance: "Kontostand: {balance} €",
    activeToursCount: "Aktive Touren: {count}",
    readyToDispatch: "Wähle einen Auftrag zum Starten",
    availableFreight: "Verfügbare Fracht",
    refreshJobs: "Aufträge neu würfeln",
    loadingJobs: "Lade Frachtbörse...",
    errorDispatching: "Fehler beim Starten der Tour.",
    legalTag: "Legal",
    inspectionRisk: "Zoll-Risiko: {risk}%",
    weightUnit: "t",
    statusDispatching: "Disponiere Auftrag...",
    latestEvents: "Ticker / Ereignisse"
  }
};

let currentLang = "de";

export function t(key, params = {}) {
  let text = translations[currentLang]?.[key] || key;
  for (const [placeholder, val] of Object.entries(params)) {
    text = text.replace(new RegExp(`{${placeholder}}`, "g"), val);
  }
  return text;
}