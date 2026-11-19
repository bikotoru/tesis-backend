import { iniciarSesion, recorrerPanel, guardarResumen } from './comun.js';

export const options = {
  stages: [{ duration: '1m', target: 50 }, { duration: '3m', target: 50 }, { duration: '1m', target: 0 }],
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
  return guardarResumen('carga', resumen);
}
