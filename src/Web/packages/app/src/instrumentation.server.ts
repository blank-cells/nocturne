import { NodeSDK } from '@opentelemetry/sdk-node';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME, ATTR_SERVICE_VERSION } from '@opentelemetry/semantic-conventions';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-proto';
import { getNodeAutoInstrumentations } from '@opentelemetry/auto-instrumentations-node';
import { OTEL_SERVICE_NAME } from '$lib/config/constants';

const endpoint = process.env.OTEL_EXPORTER_OTLP_ENDPOINT;

if (endpoint) {
	const sdk = new NodeSDK({
		resource: resourceFromAttributes({
			[ATTR_SERVICE_NAME]: OTEL_SERVICE_NAME,
			[ATTR_SERVICE_VERSION]: '1.0.0'
		}),
		traceExporter: new OTLPTraceExporter({
			url: `${endpoint}/v1/traces`,
		}),
		instrumentations: [getNodeAutoInstrumentations()]
	});

	sdk.start();

	process.on('SIGTERM', () => {
		sdk.shutdown().finally(() => process.exit(0));
	});
}
