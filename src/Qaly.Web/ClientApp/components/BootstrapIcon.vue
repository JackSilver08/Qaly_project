<script setup lang="ts">
import { computed } from "vue";
import arrowCounterclockwiseSvg from "bootstrap-icons/icons/arrow-counterclockwise.svg?raw";
import arrowRightSvg from "bootstrap-icons/icons/arrow-right.svg?raw";
import arrowsAngleExpandSvg from "bootstrap-icons/icons/arrows-angle-expand.svg?raw";
import bullseyeSvg from "bootstrap-icons/icons/bullseye.svg?raw";
import calendar3Svg from "bootstrap-icons/icons/calendar3.svg?raw";
import calendarEventSvg from "bootstrap-icons/icons/calendar-event.svg?raw";
import checkCircleSvg from "bootstrap-icons/icons/check-circle.svg?raw";
import checkCircleFillSvg from "bootstrap-icons/icons/check-circle-fill.svg?raw";
import compassSvg from "bootstrap-icons/icons/compass.svg?raw";
import diagram3Svg from "bootstrap-icons/icons/diagram-3.svg?raw";
import distributeHorizontalSvg from "bootstrap-icons/icons/distribute-horizontal.svg?raw";
import exclamationTriangleSvg from "bootstrap-icons/icons/exclamation-triangle.svg?raw";
import exclamationTriangleFillSvg from "bootstrap-icons/icons/exclamation-triangle-fill.svg?raw";
import eyeSvg from "bootstrap-icons/icons/eye.svg?raw";
import fileEarmarkBarGraphSvg from "bootstrap-icons/icons/file-earmark-bar-graph.svg?raw";
import graphUpArrowSvg from "bootstrap-icons/icons/graph-up-arrow.svg?raw";
import gridSvg from "bootstrap-icons/icons/grid.svg?raw";
import kanbanSvg from "bootstrap-icons/icons/kanban.svg?raw";
import lightningChargeSvg from "bootstrap-icons/icons/lightning-charge.svg?raw";
import link45degSvg from "bootstrap-icons/icons/link-45deg.svg?raw";
import listUlSvg from "bootstrap-icons/icons/list-ul.svg?raw";
import magicSvg from "bootstrap-icons/icons/magic.svg?raw";
import pencilSquareSvg from "bootstrap-icons/icons/pencil-square.svg?raw";
import personSvg from "bootstrap-icons/icons/person.svg?raw";
import personDashSvg from "bootstrap-icons/icons/person-dash.svg?raw";
import plusCircleSvg from "bootstrap-icons/icons/plus-circle.svg?raw";
import plusLgSvg from "bootstrap-icons/icons/plus-lg.svg?raw";
import shieldExclamationSvg from "bootstrap-icons/icons/shield-exclamation.svg?raw";
import signpostSplitSvg from "bootstrap-icons/icons/signpost-split.svg?raw";
import slidersSvg from "bootstrap-icons/icons/sliders.svg?raw";
import starsSvg from "bootstrap-icons/icons/stars.svg?raw";
import trash3Svg from "bootstrap-icons/icons/trash3.svg?raw";
import xLgSvg from "bootstrap-icons/icons/x-lg.svg?raw";

const icons: Record<string, string> = {
  "arrow-counterclockwise": arrowCounterclockwiseSvg,
  "arrow-right": arrowRightSvg,
  "arrows-angle-expand": arrowsAngleExpandSvg,
  bullseye: bullseyeSvg,
  calendar3: calendar3Svg,
  "calendar-event": calendarEventSvg,
  "check-circle": checkCircleSvg,
  "check-circle-fill": checkCircleFillSvg,
  compass: compassSvg,
  "diagram-3": diagram3Svg,
  "distribute-horizontal": distributeHorizontalSvg,
  "exclamation-triangle": exclamationTriangleSvg,
  "exclamation-triangle-fill": exclamationTriangleFillSvg,
  eye: eyeSvg,
  "file-earmark-bar-graph": fileEarmarkBarGraphSvg,
  "graph-up-arrow": graphUpArrowSvg,
  grid: gridSvg,
  kanban: kanbanSvg,
  "lightning-charge": lightningChargeSvg,
  "link-45deg": link45degSvg,
  "list-ul": listUlSvg,
  magic: magicSvg,
  "pencil-square": pencilSquareSvg,
  person: personSvg,
  "person-dash": personDashSvg,
  "plus-circle": plusCircleSvg,
  "plus-lg": plusLgSvg,
  "shield-exclamation": shieldExclamationSvg,
  "signpost-split": signpostSplitSvg,
  sliders: slidersSvg,
  stars: starsSvg,
  trash3: trash3Svg,
  "x-lg": xLgSvg,
};

const props = withDefaults(
  defineProps<{
    name: string;
    size?: number | string;
    label?: string;
  }>(),
  { size: 16 },
);

const iconSvg = computed(() => icons[props.name] ?? "");
const dimension = computed(() =>
  typeof props.size === "number" ? `${props.size}px` : props.size,
);
</script>

<template>
  <span
    class="bs-icon"
    :style="{ width: dimension, height: dimension }"
    :role="label ? 'img' : undefined"
    :aria-label="label"
    :aria-hidden="label ? undefined : 'true'"
    v-html="iconSvg"
  ></span>
</template>

<style scoped>
.bs-icon {
  display: inline-flex;
  flex: 0 0 auto;
  align-items: center;
  justify-content: center;
  color: currentColor;
  line-height: 1;
}

.bs-icon :deep(svg) {
  display: block;
  width: 100%;
  height: 100%;
  fill: currentColor;
}
</style>
