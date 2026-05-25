const { sendJson, readBody } = require("../lib/http");

module.exports = async function handler(req, res) {
  if (req.method === "OPTIONS") return sendJson(res, 200, {});
  if (req.method !== "POST") return sendJson(res, 405, { error: "Method not allowed" });

  const body = await readBody(req);
  if (body.username === "testuser" && body.password === "123456") {
    return sendJson(res, 200, {
      success: true,
      token: "jwt_token_here",
      user: { id: 1, name: "Test User" }
    });
  }

  return sendJson(res, 401, { success: false, error: "Invalid Credentials" });
};
