// Register the ESM import hook BEFORE any other imports, so auto-instrumentation
// can patch module loads. Without this, getNodeAutoInstrumentations() is a no-op
// in an ESM SvelteKit build. See https://svelte.dev/docs/kit/observability
import { createAddHookMessageChannel } from 'import-in-the-middle';
import { register } from 'node:module';

const { registerOptions } = createAddHookMessageChannel();
register('import-in-the-middle/hook.mjs', import.meta.url, registerOptions);

import { NodeSDK } from '@opentelemetry/sdk-node';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME, ATTR_SERVICE_VERSION } from '@opentelemetry/semantic-conventions';
import { getNodeAutoInstrumentations } from '@opentelemetry/auto-instrumentations-node';
import { OTEL_SERVICE_NAME } from '$lib/config/constants';

const endpoint = process.env.OTEL_EXPORTER_OTLP_ENDPOINT;

if (endpoint) {
	const sdk = new NodeSDK({
		resource: resourceFromAttributes({
			[ATTR_SERVICE_NAME]: OTEL_SERVICE_NAME,
			[ATTR_SERVICE_VERSION]: '1.0.0'
		}),
		instrumentations: [getNodeAutoInstrumentations()]
	});

	sdk.start();

	process.on('SIGTERM', () => {
		sdk.shutdown().finally(() => process.exit(0));
	});
}
