// AgriStream IoT - data stream burst for the serverless pipeline (Task e)
// Author: Genius Mhirizhonga (ST10541838)
//
// Usage: FUNC_URL=https://... FUNC_KEY=... node pipeline-burst.js
// Fires a burst of Start commands at the HTTP POST function, then one Stop per device.
// Every device should finish Idle, which proves late messages can't overwrite newer ones.

const FUNC = process.env.FUNC_URL;
const KEY = process.env.FUNC_KEY;
const COUNT = Number(process.env.COUNT || 300);
const CONCURRENCY = 25;
const devices = ['IRR-A01', 'IRR-A02', 'IRR-B01', 'IRR-B02', 'IRR-C01'];

let accepted = 0;
let failed = 0;
const times = [];

async function send(deviceId, action) {
    const started = Date.now();
    try {
        const response = await fetch(`${FUNC}/api/irrigation/commands`, {
            method: 'POST',
            headers: { 'x-functions-key': KEY, 'content-type': 'application/json' },
            body: JSON.stringify({ deviceId, action, durationMinutes: 15, requestedBy: 'burst-test' }),
            signal: AbortSignal.timeout(90000)
        });
        await response.arrayBuffer();
        if (response.status === 202) accepted++; else failed++;
        times.push(Date.now() - started);
    } catch {
        failed++;
    }
}

async function main() {
    const begin = Date.now();
    let next = 0;

    await Promise.all(Array.from({ length: CONCURRENCY }, async () => {
        while (next < COUNT) {
            const index = next++;
            await send(devices[index % devices.length], 'Start');
        }
    }));

    // Stops go last, once every Start has been accepted.
    await Promise.all(devices.map((device) => send(device, 'Stop')));

    times.sort((a, b) => a - b);
    console.log(JSON.stringify({
        commands: COUNT + devices.length,
        accepted,
        failed,
        seconds: +((Date.now() - begin) / 1000).toFixed(1),
        p50Ms: times[Math.floor(times.length * 0.5)],
        p95Ms: times[Math.floor(times.length * 0.95)]
    }));
}

main();
