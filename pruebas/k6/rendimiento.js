import { iniciarSesion, recorrerPanel, guardarResumen } from './comun.js';

export const options = {
  stages: [{ duration: '2m', target: 10 }],
  thresholds: {
    http_req_duration: ['p(95)<2000'],
    http_req_failed: ['rate<0.01'],
  },
};

export function setup() {
  return iniciarSesion();
}

export default function (datos) {
  recorrerPanel(datos);
}

export function handleSummary(resumen) {
  return guardarResumen('rendimiento', resumen);
}
