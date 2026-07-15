'use strict';

const translations = {
  en: {
    pageTitle: 'Ping Tester — See what your connection is doing',
    pageDescription: 'Ping Tester records latency, packet loss, and connection outages from a portable Windows app.',
    skip: 'Skip to content', brandTag: 'Connection recorder', navDiagnose: 'Why it helps', navWorkflow: 'How it works', navAbout: 'About',
    heroEyebrow: 'Portable · Windows 10/11 · No console', heroTitle: 'Test latency.<br><span>Find issues.</span>',
    heroLead: 'Run a repeatable connection test, watch latency and packet loss live, and keep the evidence when something goes wrong.',
    download: 'Download for Windows', kofi: 'Support on Ko-fi', releaseNotes: 'Release notes', downloadNote: 'Single portable executable · Results stay on your machine',
    liveMeasurement: 'Live measurement', live: 'Live', home: 'Home', internet: 'Internet', packetLoss: 'Packet loss', average: 'Average', maximum: 'Maximum', outages: 'Outages',
    origin: 'After badly crimped cables, misconfigured repeaters, and enough questionable home-network fixes, guessing was no longer good enough. Ping Tester exists to turn “the internet feels wrong” into evidence.',
    diagnoseEyebrow: 'Different failures. One timeline.', diagnoseTitle: 'Find where the connection<br><span>stops behaving.</span>',
    diagnoseCopy: 'Test the router, a public DNS server, or any hostname together. Separate target histories help tell a local fault from an upstream outage.',
    faultCable: 'Cable or local network', faultCableCopy: 'A failing router target points back inside the house.',
    faultRepeater: 'Repeater or Wi-Fi path', faultRepeaterCopy: 'Compare stable wired results with intermittent wireless runs.',
    faultProvider: 'Provider or internet', faultProviderCopy: 'Local targets answer while external destinations drop out.',
    workflowEyebrow: 'A test you can repeat', workflowTitle: 'Configure. Run. Compare.',
    stepConfigure: 'Choose the test', stepConfigureCopy: 'Set duration, interval, and targets from the interface.',
    stepWatch: 'Watch it live', stepWatchCopy: 'Follow latency, loss, outages, and each individual ping.',
    stepKeep: 'Keep the evidence', stepKeepCopy: 'Every run leaves CSV and JSON history you can reopen later.',
    featurePortable: 'One portable executable', featurePortableCopy: 'No installer and no loose application files to carry around.',
    featureLocal: 'Local by design', featureLocalCopy: 'Results stay on your computer under LocalAppData.',
    featureImport: 'Open old results', featureImportCopy: 'Drop in CSV or JSON files from previous tests.',
    featureLanguage: 'English and Spanish', featureLanguageCopy: 'Switch language instantly; the application remembers it.',
    aboutEyebrow: 'Behind the project', aboutTitle: 'Built from a real network headache.', aboutLoading: 'Loading profile…', aboutError: 'The profile could not be loaded.', creator: 'Creator',
    footerTag: 'Measure first. Guess less.', footerBuilt: 'Built by', sourceCode: 'Source code'
  },
  es: {
    pageTitle: 'Ping Tester — Descubre qué está haciendo tu conexión',
    pageDescription: 'Ping Tester registra latencia, pérdida de paquetes y cortes de conexión desde una aplicación portable para Windows.',
    skip: 'Saltar al contenido', brandTag: 'Registro de conexión', navDiagnose: 'Por qué ayuda', navWorkflow: 'Cómo funciona', navAbout: 'Sobre mí',
    heroEyebrow: 'Portable · Windows 10/11 · Sin consola', heroTitle: 'Analiza la latencia.<br><span>Detecta problemas.</span>',
    heroLead: 'Ejecuta una prueba repetible, observa latencia y pérdida de paquetes en directo y conserva las pruebas cuando algo falla.',
    download: 'Descargar para Windows', kofi: 'Apoyar en Ko-fi', releaseNotes: 'Notas de la versión', downloadNote: 'Un único ejecutable portable · Los resultados se quedan en tu equipo',
    liveMeasurement: 'Medición en vivo', live: 'En vivo', home: 'Casa', internet: 'Internet', packetLoss: 'Pérdida', average: 'Media', maximum: 'Máxima', outages: 'Cortes',
    origin: 'Después de cables mal crimpados, repetidores mal configurados y suficientes arreglos domésticos cuestionables, seguir adivinando dejó de ser una opción. Ping Tester convierte “creo que internet va mal” en pruebas.',
    diagnoseEyebrow: 'Fallos distintos. Una sola línea temporal.', diagnoseTitle: 'Descubre dónde deja<br><span>de funcionar la conexión.</span>',
    diagnoseCopy: 'Prueba a la vez el router, un DNS público o cualquier hostname. El histórico separado por destino permite distinguir un fallo local de una caída externa.',
    faultCable: 'Cable o red local', faultCableCopy: 'Si falla el destino del router, el problema apunta al interior de casa.',
    faultRepeater: 'Repetidor o ruta Wi-Fi', faultRepeaterCopy: 'Compara resultados estables por cable con pruebas inalámbricas intermitentes.',
    faultProvider: 'Proveedor o internet', faultProviderCopy: 'Los destinos locales responden mientras los externos dejan de hacerlo.',
    workflowEyebrow: 'Una prueba que puedes repetir', workflowTitle: 'Configura. Ejecuta. Compara.',
    stepConfigure: 'Elige la prueba', stepConfigureCopy: 'Configura duración, intervalo y destinos desde la interfaz.',
    stepWatch: 'Obsérvala en directo', stepWatchCopy: 'Sigue latencia, pérdida, cortes y cada ping individual.',
    stepKeep: 'Conserva las pruebas', stepKeepCopy: 'Cada ejecución deja un histórico CSV y JSON que puedes volver a abrir.',
    featurePortable: 'Un único ejecutable portable', featurePortableCopy: 'Sin instalador ni archivos sueltos de la aplicación.',
    featureLocal: 'Local por diseño', featureLocalCopy: 'Los resultados permanecen en tu equipo dentro de LocalAppData.',
    featureImport: 'Abre resultados antiguos', featureImportCopy: 'Carga archivos CSV o JSON de pruebas anteriores.',
    featureLanguage: 'Inglés y español', featureLanguageCopy: 'Cambia el idioma al instante; la aplicación lo recuerda.',
    aboutEyebrow: 'Detrás del proyecto', aboutTitle: 'Creado a partir de un problema de red real.', aboutLoading: 'Cargando perfil…', aboutError: 'No se pudo cargar el perfil.', creator: 'Creador',
    footerTag: 'Mide primero. Adivina menos.', footerBuilt: 'Creado por', sourceCode: 'Código fuente'
  }
};

const linkIcons = {
  github: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 .7a11.5 11.5 0 0 0-3.64 22.41c.58.11.79-.25.79-.56v-2.24c-3.22.7-3.9-1.37-3.9-1.37-.53-1.34-1.29-1.7-1.29-1.7-1.05-.72.08-.7.08-.7 1.16.08 1.77 1.19 1.77 1.19 1.04 1.77 2.72 1.26 3.38.96.1-.75.4-1.26.74-1.55-2.57-.29-5.27-1.29-5.27-5.68 0-1.26.45-2.28 1.19-3.09-.12-.29-.52-1.47.11-3.05 0 0 .97-.31 3.16 1.18A10.98 10.98 0 0 1 12 6.09c.98 0 1.94.13 2.85.38 2.2-1.49 3.17-1.18 3.17-1.18.63 1.58.23 2.76.11 3.05.74.81 1.19 1.83 1.19 3.09 0 4.4-2.71 5.38-5.29 5.67.42.36.79 1.07.79 2.16v3.29c0 .31.21.68.8.56A11.5 11.5 0 0 0 12 .7Z"/></svg>',
  linkedin: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5.34 7.72H1.15V21h4.19V7.72ZM3.25 1a2.43 2.43 0 1 0 0 4.86 2.43 2.43 0 0 0 0-4.86ZM21.85 13.39c0-4-2.14-5.86-5-5.86a4.31 4.31 0 0 0-3.91 2.15V7.72H8.75V21h4.19v-6.58c0-1.74.33-3.42 2.48-3.42 2.12 0 2.15 1.98 2.15 3.53V21h4.28v-7.61Z"/></svg>',
  kofi: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 5h13v10a5 5 0 0 1-5 5H9a5 5 0 0 1-5-5V5Zm13 2v6h1a3 3 0 0 0 0-6h-1ZM8.1 9.1c-1.8 0-2.2 2.3-.8 3.3l3.2 2.3 3.2-2.3c1.4-1 .9-3.3-.8-3.3-1 0-1.6.7-2.4 1.4-.8-.7-1.4-1.4-2.4-1.4Z"/></svg>'
};

const state = {
  language: 'en',
  about: null
};

function projectCoordinates() {
  const pathParts = location.pathname.split('/').filter(Boolean);
  if (location.hostname.endsWith('.github.io') && pathParts.length > 0) {
    return { owner: location.hostname.split('.')[0], repository: pathParts[0] };
  }
  return { owner: 'AlexAdiaconitei', repository: 'ping-tester' };
}

async function configureProjectLinks() {
  const { owner, repository } = projectCoordinates();
  const projectUrl = `https://github.com/${owner}/${repository}`;
  const downloadButton = document.getElementById('downloadButton');
  downloadButton.href = `${projectUrl}/releases/latest/download/PingTester-v1.0.0-win-x64.exe`;
  document.getElementById('releaseNotesButton').href = `${projectUrl}/releases/latest`;

  const sourceLink = document.querySelector('[data-i18n="sourceCode"]');
  if (sourceLink) sourceLink.href = projectUrl;

  try {
    const response = await fetch(`https://api.github.com/repos/${owner}/${repository}/releases/latest`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);

    const release = await response.json();
    const executable = release.assets.find(asset => asset.name.toLowerCase().endsWith('.exe'));
    if (!executable) throw new Error('The latest release has no Windows executable');

    downloadButton.href = executable.browser_download_url;
  } catch (error) {
    console.error('Unable to resolve the latest release executable', error);
  }
}

function translatePage(language, persist = true) {
  state.language = language === 'es' ? 'es' : 'en';
  document.documentElement.lang = state.language;
  const dictionary = translations[state.language];

  document.querySelectorAll('[data-i18n]').forEach(element => {
    const key = element.dataset.i18n;
    if (dictionary[key]) element.textContent = dictionary[key];
  });
  document.querySelectorAll('[data-i18n-html]').forEach(element => {
    const key = element.dataset.i18nHtml;
    if (dictionary[key]) element.innerHTML = dictionary[key];
  });
  document.querySelectorAll('[data-language]').forEach(button => {
    const selected = button.dataset.language === state.language;
    button.classList.toggle('active', selected);
    button.setAttribute('aria-pressed', String(selected));
  });

  document.title = dictionary.pageTitle;
  document.querySelector('meta[name="description"]').content = dictionary.pageDescription;
  if (persist) localStorage.setItem('pingTesterLandingLanguage', state.language);
  renderAbout();
}

function element(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function renderAbout() {
  if (!state.about) return;
  const data = state.about;
  const content = document.querySelector('.about-content');
  const kicker = element('p', 'about-kicker', translations[state.language].creator);
  const name = element('h3', '', data.name);
  const bio = element('p', 'about-bio', data.bio[state.language]);
  const facts = element('ul', 'about-facts');
  data.facts[state.language].forEach(fact => facts.append(element('li', '', fact)));
  const links = element('div', 'about-links');

  data.links.forEach(item => {
    const link = element('a', '', item.label);
    link.href = item.url;
    link.target = '_blank';
    link.rel = 'noreferrer';
    link.insertAdjacentHTML('afterbegin', linkIcons[item.icon] || '');
    links.append(link);
  });

  content.replaceChildren(kicker, name, bio, facts, links);
  document.querySelector('.about-photo').alt = data.name;
}

async function loadAbout() {
  try {
    const response = await fetch('about.json', { cache: 'no-cache' });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    state.about = await response.json();
    renderAbout();
  } catch (error) {
    const content = document.querySelector('.about-content');
    content.replaceChildren(element('p', 'about-error', translations[state.language].aboutError));
    console.error('Unable to load about.json', error);
  }
}

document.querySelectorAll('[data-language]').forEach(button => {
  button.addEventListener('click', () => translatePage(button.dataset.language));
});

configureProjectLinks();
const savedLanguage = localStorage.getItem('pingTesterLandingLanguage');
translatePage(savedLanguage === 'es' ? 'es' : 'en', false);
loadAbout();
