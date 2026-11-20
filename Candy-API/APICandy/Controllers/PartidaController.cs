using CandyApi.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Cors;
using static CandyApi.Entity.Elemento;

namespace CandyApi.Controllers
{
    public class CandyBoardEngine
    {
        public const int GridSize = 9;
        public const int CellCount = GridSize * GridSize;

        private static readonly Color[] Palette = (Color[])Enum.GetValues(typeof(Color));
        private readonly ConcurrentDictionary<int, Partida> _partidas = new ConcurrentDictionary<int, Partida>();
        private readonly object _rngLock = new object();
        private readonly Random _random;
        private int _nextId;

        public CandyBoardEngine(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        private Color NextColor()
        {
            lock (_rngLock)
            {
                return Palette[_random.Next(Palette.Length)];
            }
        }

        public static bool IsValidIndex(int index)
        {
            return index >= 0 && index < CellCount;
        }

        public static bool AreAdjacent(int a, int b)
        {
            if (!IsValidIndex(a) || !IsValidIndex(b) || a == b)
            {
                return false;
            }
            int rowA = a / GridSize;
            int colA = a % GridSize;
            int rowB = b / GridSize;
            int colB = b % GridSize;
            return Math.Abs(rowA - rowB) + Math.Abs(colA - colB) == 1;
        }

        public Partida CreatePartida()
        {
            var elementos = new List<Elemento>(CellCount);
            for (int i = 0; i < CellCount; i++)
            {
                int row = i / GridSize;
                int col = i % GridSize;
                Color chosen = NextColor();
                int guard = 0;
                while (guard < 16 && CausesImmediateMatch(elementos, row, col, chosen))
                {
                    chosen = NextColor();
                    guard++;
                }
                elementos.Add(new Elemento { id = i, color = chosen });
            }

            int id = Interlocked.Increment(ref _nextId);
            var partida = new Partida
            {
                id = id,
                elementos = elementos
            };
            _partidas[id] = partida;
            return ClonePartida(partida);
        }

        public Partida GetPartida(int id)
        {
            Partida found;
            return _partidas.TryGetValue(id, out found) ? ClonePartida(found) : null;
        }

        public bool TrySwap(int partidaId, int movimiento1, int movimiento2, bool requireAdjacency, out Partida updated, out int matchedCells, out string error)
        {
            updated = null;
            matchedCells = 0;
            error = null;

            Partida partida;
            if (!_partidas.TryGetValue(partidaId, out partida) || partida.elementos == null || partida.elementos.Count != CellCount)
            {
                error = "No existe la partida indicada.";
                return false;
            }

            if (!IsValidIndex(movimiento1) || !IsValidIndex(movimiento2))
            {
                error = "Los índices de movimiento deben estar entre 0 y 80.";
                return false;
            }

            if (movimiento1 == movimiento2)
            {
                error = "Debe seleccionar dos casillas distintas para intercambiar.";
                return false;
            }

            if (requireAdjacency && !AreAdjacent(movimiento1, movimiento2))
            {
                error = "Las casillas seleccionadas deben ser adyacentes (horizontal o vertical).";
                return false;
            }

            lock (partida)
            {
                Elemento first = partida.elementos.FirstOrDefault(x => x.id == movimiento1);
                Elemento second = partida.elementos.FirstOrDefault(x => x.id == movimiento2);
                if (first == null || second == null)
                {
                    error = "No se encontraron las casillas solicitadas en el tablero.";
                    return false;
                }

                Color tmpColor = first.color;
                first.color = second.color;
                second.color = tmpColor;

                matchedCells = ResolveMatchesAndRefill(partida.elementos);
                partida.elementos = partida.elementos.OrderBy(x => x.id).ToList();
                updated = ClonePartida(partida);
                return true;
            }
        }

        public int ResolveMatchesAndRefill(List<Elemento> elementos)
        {
            int totalCleared = 0;
            for (int cascade = 0; cascade < 12; cascade++)
            {
                HashSet<int> matches = FindMatches(elementos);
                if (matches.Count == 0)
                {
                    break;
                }

                totalCleared += matches.Count;
                // Gravity per column
                for (int col = 0; col < GridSize; col++)
                {
                    var survivors = new List<Color>(GridSize);
                    for (int row = GridSize - 1; row >= 0; row--)
                    {
                        int idx = row * GridSize + col;
                        if (!matches.Contains(idx))
                        {
                            survivors.Add(elementos[idx].color);
                        }
                    }

                    int writeIdx = 0;
                    for (int row = GridSize - 1; row >= 0; row--)
                    {
                        int idx = row * GridSize + col;
                        elementos[idx].color = writeIdx < survivors.Count ? survivors[writeIdx++] : NextColor();
                    }
                }
            }
            return totalCleared;
        }

        public static HashSet<int> FindMatches(IList<Elemento> elementos)
        {
            var matched = new HashSet<int>();
            if (elementos == null || elementos.Count != CellCount)
            {
                return matched;
            }

            // Horizontal runs >= 3
            for (int row = 0; row < GridSize; row++)
            {
                int col = 0;
                while (col < GridSize)
                {
                    int start = col;
                    Color color = elementos[row * GridSize + col].color;
                    while (col < GridSize && elementos[row * GridSize + col].color == color)
                    {
                        col++;
                    }
                    if (col - start >= 3)
                    {
                        for (int k = start; k < col; k++)
                        {
                            matched.Add(row * GridSize + k);
                        }
                    }
                }
            }

            // Vertical runs >= 3
            for (int col = 0; col < GridSize; col++)
            {
                int row = 0;
                while (row < GridSize)
                {
                    int start = row;
                    Color color = elementos[row * GridSize + col].color;
                    while (row < GridSize && elementos[row * GridSize + col].color == color)
                    {
                        row++;
                    }
                    if (row - start >= 3)
                    {
                        for (int k = start; k < row; k++)
                        {
                            matched.Add(k * GridSize + col);
                        }
                    }
                }
            }

            return matched;
        }

        private static bool CausesImmediateMatch(List<Elemento> elementos, int row, int col, Color candidate)
        {
            if (col >= 2)
            {
                if (elementos[row * GridSize + (col - 1)].color == candidate &&
                    elementos[row * GridSize + (col - 2)].color == candidate)
                {
                    return true;
                }
            }

            if (row >= 2)
            {
                if (elementos[(row - 1) * GridSize + col].color == candidate &&
                    elementos[(row - 2) * GridSize + col].color == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private static Partida ClonePartida(Partida source)
        {
            return new Partida
            {
                id = source.id,
                elementos = source.elementos.Select(e => new Elemento { id = e.id, color = e.color }).ToList()
            };
        }
    }

    public class PartidaController : ApiController
    {
        private static readonly CandyBoardEngine Engine = new CandyBoardEngine();

        [EnableCors(origins: "*", headers: "*", methods: "*")]
        // GET api/Partida/1
        public Task<IHttpActionResult> GetPartida(int id)
        {
            Partida partida = Engine.GetPartida(id);
            if (partida == null)
            {
                return Task.FromResult<IHttpActionResult>(NotFound());
            }
            return Task.FromResult<IHttpActionResult>(Json(partida));
        }

        [EnableCors(origins: "*", headers: "*", methods: "*")]
        // POST api/Partida
        public Task<IHttpActionResult> PostPartida([FromBody] Usuario usuario)
        {
            Partida creada = Engine.CreatePartida();
            return Task.FromResult<IHttpActionResult>(Json(creada));
        }

        [EnableCors(origins: "*", headers: "*", methods: "*")]
        // PUT api/Partida/1
        public Task<IHttpActionResult> PutPartida(int id, [FromBody] UsuarioMovimientos usuarioMovimientos)
        {
            if (usuarioMovimientos == null)
            {
                return Task.FromResult<IHttpActionResult>(BadRequest("Debe enviar los movimientos a ejecutar."));
            }

            Partida updated;
            int matched;
            string error;
            if (!Engine.TrySwap(id, usuarioMovimientos.movimiento1, usuarioMovimientos.movimiento2, true, out updated, out matched, out error))
            {
                if (error != null && error.StartsWith("No existe", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult<IHttpActionResult>(NotFound());
                }
                return Task.FromResult<IHttpActionResult>(BadRequest(error ?? "Movimiento inválido."));
            }

            return Task.FromResult<IHttpActionResult>(Json(updated));
        }
    }
}
