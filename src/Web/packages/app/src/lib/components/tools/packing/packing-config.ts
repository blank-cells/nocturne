export type SupplyItemMode = "interval" | "insulin" | "flat";

export interface SupplyItemConfig {
  /** Unique key for this item */
  id: string;
  /** Display name */
  label: string;
  /** How quantity is calculated */
  mode: SupplyItemMode;
  /** Default change interval in days (for interval mode) */
  defaultInterval?: number;
  /** Default buffer (extra count for interval/flat, percentage 0-1 for insulin) */
  defaultBuffer: number;
  /** Default container size in units (insulin mode only) */
  defaultContainerSize?: number;
  /** Which device event types provide a hint for this item's interval */
  hintEventTypes?: string[];
  /** Whether this item is enabled by default */
  defaultEnabled: boolean;
}

export interface SupplyCategoryConfig {
  id: string;
  label: string;
  icon: string;
  items: SupplyItemConfig[];
}

export const categories: SupplyCategoryConfig[] = [
  {
    id: "insulin",
    label: "Insulin",
    icon: "Syringe",
    items: [
      {
        id: "rapid-pens",
        label: "Rapid-acting pens/vials",
        mode: "insulin",
        defaultBuffer: 0.5,
        defaultContainerSize: 300,
        defaultEnabled: true,
      },
      {
        id: "long-pens",
        label: "Long-acting pens/vials",
        mode: "insulin",
        defaultBuffer: 0.5,
        defaultContainerSize: 300,
        defaultEnabled: false,
      },
      {
        id: "pump-reservoirs",
        label: "Pump cartridges/reservoirs",
        mode: "interval",
        defaultInterval: 3,
        defaultBuffer: 1,
        hintEventTypes: ["InsulinChange", "ReservoirChange"],
        defaultEnabled: false,
      },
    ],
  },
  {
    id: "cgm",
    label: "CGM",
    icon: "Activity",
    items: [
      {
        id: "sensors",
        label: "Sensors",
        mode: "interval",
        defaultInterval: 10,
        defaultBuffer: 1,
        hintEventTypes: ["SensorStart", "SensorChange"],
        defaultEnabled: true,
      },
      {
        id: "transmitters",
        label: "Transmitters",
        mode: "flat",
        defaultBuffer: 1,
        defaultEnabled: false,
      },
    ],
  },
  {
    id: "pump",
    label: "Pump",
    icon: "Cpu",
    items: [
      {
        id: "infusion-sets",
        label: "Infusion sets",
        mode: "interval",
        defaultInterval: 3,
        defaultBuffer: 1,
        hintEventTypes: ["SiteChange"],
        defaultEnabled: true,
      },
      {
        id: "cannulas",
        label: "Cannulas",
        mode: "interval",
        defaultInterval: 3,
        defaultBuffer: 1,
        hintEventTypes: ["CannulaChange"],
        defaultEnabled: true,
      },
      {
        id: "pump-batteries",
        label: "Batteries",
        mode: "flat",
        defaultBuffer: 1,
        defaultEnabled: false,
      },
    ],
  },
  {
    id: "testing",
    label: "Testing",
    icon: "TestTube",
    items: [
      {
        id: "test-strips",
        label: "Test strips",
        mode: "flat",
        defaultBuffer: 0,
        defaultEnabled: false,
      },
      {
        id: "lancets",
        label: "Lancets",
        mode: "flat",
        defaultBuffer: 0,
        defaultEnabled: false,
      },
      {
        id: "alcohol-swabs",
        label: "Alcohol swabs",
        mode: "flat",
        defaultBuffer: 0,
        defaultEnabled: false,
      },
      {
        id: "control-solution",
        label: "Control solution",
        mode: "flat",
        defaultBuffer: 0,
        defaultEnabled: false,
      },
    ],
  },
  {
    id: "emergency",
    label: "Emergency",
    icon: "ShieldAlert",
    items: [
      {
        id: "glucagon",
        label: "Glucagon",
        mode: "flat",
        defaultBuffer: 0,
        defaultEnabled: true,
      },
      {
        id: "glucose-tabs",
        label: "Glucose tabs / juice",
        mode: "flat",
        defaultBuffer: 0,
        defaultEnabled: true,
      },
      {
        id: "ketone-strips",
        label: "Ketone strips",
        mode: "flat",
        defaultBuffer: 0,
        defaultEnabled: false,
      },
    ],
  },
];
