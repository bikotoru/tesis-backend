import { iniciarSesion, recorrerPanel, guardarResumen } from './comun.js';

export const options = {
  stages: [{ duration: '1m', target: 50 }, { duration: '1m', target: 100 }, { duration: '1m', target: 200 }, { duration: '1m', target: 300 }, { duration: '1m', target: 0 }],
  thresholds: {
    http_req_duration: ['p(95)<10000'],
    http_req_failed: ['rate<0.10'],
  },
};

export function setup() {
  return iniciarSesion();
}

export default function (datos) {
  recorrerPanel(datos);
}

export function handleSummary(resumen) {
  return guardarResumen('estres', resumen);
}
