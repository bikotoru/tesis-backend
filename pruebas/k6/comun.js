import http from 'k6/http';
import crypto from 'k6/crypto';
import { check, sleep } from 'k6';

const base = __ENV.BASE_URL || 'http://host.docker.internal:8080';
const correo = __ENV.CORREO;
const clave = __ENV.CLAVE;
const secreto = __ENV.SECRETO;
const usarWebhook = __ENV.WEBHOOK === 'si';

export function iniciarSesion() {
  const cuerpo = JSON.stringify({ correo: correo, clave: clave });
  const opciones = { headers: { 'Content-Type': 'application/json' } };
  const respuesta = http.post(`${base}/api/auth/ingresar`, cuerpo, opciones);
  if (respuesta.status !== 200) {
    throw new Error(`No se pudo iniciar sesión: ${respuesta.status}`);
  }
  return { token: respuesta.json('token') };
}

function consultar(ruta, nombre, token) {
  const opciones = { headers: { Authorization: `Bearer ${token}` }, tags: { name: nombre } };
  const respuesta = http.get(`${base}${ruta}`, opciones);
  check(respuesta, { 'respuesta 200': r => r.status === 200 });
}

function avisoDeWhatsApp() {
  const numero = `569${String(__VU).padStart(8, '0')}`;
  const mensaje = {
    from: numero,
    id: `k6-${__VU}-${__ITER}-${Date.now()}`,
    timestamp: String(Math.floor(Date.now() / 1000)),
    type: 'text',
    text: { body: 'Hola, ¿tienen látex?' },
  };
  const valor = { contacts: [{ wa_id: numero, profile: { name: 'Prueba k6' } }], messages: [mensaje] };
  return JSON.stringify({ entry: [{ changes: [{ value: valor }] }] });
}

function enviarWebhook() {
  const cuerpo = avisoDeWhatsApp();
  const firma = crypto.hmac('sha256', secreto, cuerpo, 'hex');
  const opciones = {
    headers: { 'Content-Type': 'application/json', 'X-Hub-Signature-256': `sha256=${firma}` },
    tags: { name: 'webhook' },
  };
  const respuesta = http.post(`${base}/api/whatsapp`, cuerpo, opciones);
  check(respuesta, { 'webhook 200': r => r.status === 200 });
}

export function recorrerPanel(datos) {
  consultar('/api/productos?busqueda=latex', 'productos', datos.token);
  consultar('/api/cotizaciones', 'cotizaciones', datos.token);
  consultar('/api/conversaciones', 'conversaciones', datos.token);
  if (usarWebhook) {
    enviarWebhook();
  }
  sleep(1);
}

export function guardarResumen(nombre, resumen) {
  const salida = {};
  salida[`/resultados/${nombre}.json`] = JSON.stringify(resumen, null, 2);
  return salida;
}
