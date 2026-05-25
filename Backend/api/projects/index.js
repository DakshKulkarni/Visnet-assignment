const { sendJson } = require("../../lib/http");

module.exports = function handler(req, res) {
  if (req.method === "OPTIONS") return sendJson(res, 200, {});
  if (req.method !== "GET") return sendJson(res, 405, { error: "Method not allowed" });

  return sendJson(res, 200, {
    projects: [
      { id: 1, name: "Project A" },
      { id: 2, name: "Project B" },
      { id: 3, name: "Project C" },
      { id: 4, name: "Project D" }
    ]
  });
};
