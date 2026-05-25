const { sendJson } = require("../../../lib/http");

const floorsByProject = {
  1: ["Floor 1", "Floor 2", "Floor 3", "Floor 4"],
  2: ["Floor A", "Floor B"],
  3: ["Ground", "1", "2", "3", "4", "5"],
  4: ["Basement", "Ground", "Mezzanine"]
};

module.exports = function handler(req, res) {
  if (req.method === "OPTIONS") return sendJson(res, 200, {});
  if (req.method !== "GET") return sendJson(res, 405, { error: "Method not allowed" });

  const id = Number(req.query.id);
  return sendJson(res, 200, { floors: floorsByProject[id] || [] });
};
