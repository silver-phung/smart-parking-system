/**
 * Calculates operational capacity metrics for the Admin Dashboard.
 * Ensures that physical slots deployed, design capacity, and available slots
 * strictly reflect active lots and differentiate real slots from unconfigured lots.
 *
 * @param {Array<Object>} lots
 * @returns {Object}
 */
export function calculateDashboardSummary(lots = []) {
  const activeLots = lots.filter((lot) => lot.isActive);

  const physicalSlots = activeLots.reduce(
    (sum, lot) => sum + (lot.physicalSlotsCount ?? lot.totalCapacity ?? 0),
    0,
  );

  const configuredCapacity = activeLots.reduce(
    (sum, lot) => sum + (lot.configuredCapacity ?? lot.totalCapacity ?? 0),
    0,
  );

  const capacity = activeLots.reduce(
    (sum, lot) => sum + (lot.totalCapacity || 0),
    0,
  );

  const occupied = activeLots.reduce(
    (sum, lot) => sum + (lot.occupiedSlotsCount || 0),
    0,
  );

  const available = activeLots.reduce(
    (sum, lot) => sum + (lot.availableSlotsCount || 0),
    0,
  );

  return {
    physicalSlots,
    configuredCapacity,
    capacity,
    occupied,
    available,
    openLots: activeLots.length,
    occupancy:
      physicalSlots > 0
        ? Math.min(100, Math.round((occupied / physicalSlots) * 100))
        : capacity > 0
          ? Math.min(100, Math.round((occupied / capacity) * 100))
          : 0,
  };
}
