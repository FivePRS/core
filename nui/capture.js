const gameCapture = (() => {
  const maxWidth = 1280;
  const warmupFrames = 3;

  const vertexSource = `
    attribute vec2 position;
    varying vec2 uv;
    void main() {
      uv = (position + 1.0) / 2.0;
      gl_Position = vec4(position, 0.0, 1.0);
    }`;

  const fragmentSource = `
    precision mediump float;
    varying vec2 uv;
    uniform sampler2D frame;
    void main() {
      gl_FragColor = vec4(texture2D(frame, uv).rgb, 1.0);
    }`;

  let canvas = null;
  let gl = null;
  let busy = false;

  function compile(type, source) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    return gl.getShaderParameter(shader, gl.COMPILE_STATUS) ? shader : null;
  }

  function setup() {
    if (gl) return true;

    canvas = document.createElement("canvas");
    gl = canvas.getContext("webgl", { preserveDrawingBuffer: true, antialias: false, alpha: false });
    if (!gl) return false;

    const vertex = compile(gl.VERTEX_SHADER, vertexSource);
    const fragment = compile(gl.FRAGMENT_SHADER, fragmentSource);
    if (!vertex || !fragment) {
      gl = null;
      return false;
    }

    const program = gl.createProgram();
    gl.attachShader(program, vertex);
    gl.attachShader(program, fragment);
    gl.linkProgram(program);
    gl.useProgram(program);

    gl.bindBuffer(gl.ARRAY_BUFFER, gl.createBuffer());
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 1, -1, -1, 1, -1, 1, 1, -1, 1, 1]), gl.STATIC_DRAW);
    const position = gl.getAttribLocation(program, "position");
    gl.enableVertexAttribArray(position);
    gl.vertexAttribPointer(position, 2, gl.FLOAT, false, 0, 0);

    gl.bindTexture(gl.TEXTURE_2D, gl.createTexture());
    gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGB, 1, 1, 0, gl.RGB, gl.UNSIGNED_BYTE, new Uint8Array(3));
    gl.texParameterf(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.texParameterf(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.MIRRORED_REPEAT);
    gl.texParameterf(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.REPEAT);
    gl.uniform1i(gl.getUniformLocation(program, "frame"), 0);
    return true;
  }

  function nextFrame() {
    return new Promise((resolve) => {
      const timer = setTimeout(resolve, 100);
      requestAnimationFrame(() => {
        clearTimeout(timer);
        resolve();
      });
    });
  }

  function draw() {
    const width = Math.max(1, Math.round(window.innerWidth * (window.devicePixelRatio || 1)));
    const height = Math.max(1, Math.round(window.innerHeight * (window.devicePixelRatio || 1)));
    if (canvas.width !== width || canvas.height !== height) {
      canvas.width = width;
      canvas.height = height;
    }

    gl.viewport(0, 0, width, height);
    gl.drawArrays(gl.TRIANGLES, 0, 6);
  }

  function isBlank(source) {
    const probe = document.createElement("canvas");
    probe.width = 16;
    probe.height = 16;
    const context = probe.getContext("2d");
    context.drawImage(source, 0, 0, 16, 16);
    const pixels = context.getImageData(0, 0, 16, 16).data;
    for (let i = 0; i < pixels.length; i += 4) {
      if (pixels[i] > 8 || pixels[i + 1] > 8 || pixels[i + 2] > 8) return false;
    }
    return true;
  }

  function encode(quality) {
    const scale = Math.min(1, maxWidth / canvas.width);
    const output = document.createElement("canvas");
    output.width = Math.round(canvas.width * scale);
    output.height = Math.round(canvas.height * scale);
    output.getContext("2d").drawImage(canvas, 0, 0, output.width, output.height);
    return output.toDataURL("image/jpeg", quality);
  }

  async function capture(quality = 0.9) {
    if (busy || !setup()) return null;
    busy = true;

    try {
      for (let i = 0; i < warmupFrames; i++) {
        await nextFrame();
        draw();
      }

      return isBlank(canvas) ? null : encode(quality);
    } catch {
      return null;
    } finally {
      busy = false;
    }
  }

  window.addEventListener("message", async (event) => {
    const id = event.data?.fiveprsCapture;
    if (typeof id !== "number") return;

    const data = await capture();
    post("fiveprsCaptured", { id, data: data ?? "" });
  });

  return { capture };
})();
