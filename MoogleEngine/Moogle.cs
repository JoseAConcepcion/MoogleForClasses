using System.Text.RegularExpressions;

namespace MoogleEngine;
public static class Moogle
{
    private static List<string> _querySplit = new List<string>();
    public static SearchResult Query(string query)
    {
        if (query.Length == 0)
        {
            SearchItem[] vacio = new SearchItem[] { new SearchItem(
                "no encontramos coincidencias 😪",
                "busque otra vez, pero pruebe escribir 😉", 0.9f) };
            return new SearchResult(vacio, query);

        }
        //se determina si el query tiene operadores o no y se limpia para realizar la busqueda, y se inicializa un array de bools
        if (!CheckOperators(query))
        {
            _querySplit = Limpiar(query); //busqueda normal
            dictionarysBool = DoDiccsBool();
        }
        else
        {
            _querySplit = Limpiar(query);

            if (CheckOperatorBool(query, "~"))
            {
                _nearbyBool = true;
                _queryNearby = CleanNearbyOperator(_querySplit, "~");
                _queryNearby = CleanOperatorRelevanceExtra(_queryNearby, "!");
                _queryNearby = CleanOperatorRelevanceExtra(_queryNearby, "*");
                _queryNearby = CleanOperatorRelevanceExtra(_queryNearby, "^");
                _querySplit = CleanOperatorX(_querySplit, "~");
                _nearbyArray = DoNearbyFinal(_queryNearby);
            }

            if (CheckOperatorBool(query, "*"))
            {
                _relevanceBool = true;
                _queryRelevance = Operator(_querySplit, "*");
                _queryRelevance = CleanOperatorRelevanceExtra(_queryNearby, "!");
                _queryRelevance = CleanOperatorRelevanceExtra(_queryNearby, "^");
                _querySplit = CleanOperatorRelevance(_querySplit, "*");
            }

            if (CheckOperatorBool(query, "!"))
            {
                _notBool = true;
                _queryNot = Operator(_querySplit, "!");
                _querySplit = CleanOperatorsFull(_querySplit, "!");

            }
            if (CheckOperatorBool(query, "^"))
            {
                _andBool = true;
                _queryAnd = Operator(_querySplit, "^");
                _querySplit = CleanOperatorsFull(_querySplit, "^");
            }

            dictionarysBool = DoDiccsBool();
        }

        //se calcula el tfidf de cada documento por query y se organiza los valores de mayor a menor

        Dictionary<string, Dictionary<string, float>> queryTitleTfidf = DoTfidf(_querySplit);
        Dictionary<string, float> final = Final_tfidf(queryTitleTfidf);
        List<string> order = Organizar_Values(final);

        SearchItem[] items = new SearchItem[New_ItemL(final, order)];
        for (int i = 0; i < New_ItemL(final, order); i++)
        {

            if (!dictionarysBool[order[i]] && final[order[i]] != 0)
            {
                string snip = Snippet(order[i], _querySplit);
                items[i] = new SearchItem(order[i], snip, final[order[i]]);
            }
        }

        var suggestion = Suggestion();
        if (New_ItemL(final, order) == 0)
        {
            SearchItem[] vacio = new SearchItem[] { new SearchItem(
            "No encontramos coincidencias que satisfagan su búsqueda 😔",
             "recuerde usar los operadores apropidamente 😉", 0.9f) };
            return new SearchResult(vacio, suggestion);

        }

        Cleanup(); //se limpian los valores

        return new SearchResult(items, suggestion);
    }

    #region Carga de Archivos

    private static string _contentFolder = Path.Join(Environment.CurrentDirectory, "..", "Content/"); //direccion donde estaran los archivos
    private static string[] _directories = ObtenerDirectorios(_contentFolder);
    private static List<string> _queryNot = new List<string>();
    private static int _numberFiles = _directories.Length;
    private static int[] _relevance = new int[20];
    private static Dictionary<string, List<string>> _titleDocs = Load();
    private static string[] ObtenerDirectorios(string carga)
    {   //se buscan todos los txt
        string[] directorios = Directory.GetFiles(carga, "*.txt", SearchOption.AllDirectories);
        _numberFiles = directorios.Length;
        return directorios;
    }
    private static Dictionary<string, List<string>> Load()
    {   //se cargan las palabras de cada documento sin caracteres especiales para optimizar la busqueda
        Dictionary<string, List<string>> titleDocsReturn = new Dictionary<string, List<string>>();
        List<string> docs = new List<string>();
        for (int i = 0; i < _directories.Length; i++)
        {
            StreamReader reader = new StreamReader(_directories[i]);
            string temp = reader.ReadToEnd();
            docs = Limpiar_Docs(temp);
            titleDocsReturn.Add(_directories[i], docs);
        }
        return titleDocsReturn;
    }
    private static List<string> Limpiar(string words)
    {   //limpieza del query
        return words.ToLower().Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
    }
    private static List<string> Limpiar_Docs(string words)
    {       //limpieza de documentos
        string[] wordsSplit = words.ToLower().Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        string[] cleaned = new string[wordsSplit.Length];
        for (int i = 0; i < wordsSplit.Length; i++)
        {
            cleaned[i] = Regex.Replace(wordsSplit[i], @"[""!@#$%^&*()¿?<>.,;«»{}'¡0123456789' '_-]+", "");

        }

        return cleaned.ToList<string>();
    }
    #endregion

    #region TFIDF

    private static Dictionary<string, Dictionary<string, float>> DoTfidf(List<string> querySplit)
    {
        //calculo de tfidf

        Dictionary<string, Dictionary<string, float>> queryTitleTfidfReturn = new Dictionary<string, Dictionary<string, float>> { };
        float tfIdfTemp = 0;
        for (int i = 0; i < querySplit.Count(); i++) //se recorren las palabras del query
        {

            Dictionary<string, float> tfidfDocs = new Dictionary<string, float>();

            for (int k = 0; k < _directories.Length; k++) //se recorren los documentos y se inicializa la cuenta de palabras
            {
                float countQuery = 0;
                if (!dictionarysBool[_directories[k]]) //se determina si el documento es valido para analizar o no
                {

                    for (int z = 0; z < _titleDocs[_directories[k]].Count(); z++)
                    {
                        //coincidencia
                        if ((querySplit[i] == _titleDocs[_directories[k]][z])) countQuery++;
                        if (z == _titleDocs[_directories[k]].Count() - 1)
                        {
                            //se llevan a cabo las multiplicaciones en correspondencia con la relevancia de cada archivo

                            tfIdfTemp = Tf(countQuery, _titleDocs[_directories[k]].Count) * Idf(_numberFiles, CalculateIDF_extra(querySplit[i]));

                            if (_nearbyBool) tfIdfTemp = tfIdfTemp + (_nearbyArray[k] * 2);
                            if (_relevanceBool && _relevance[i] != 0 && tfIdfTemp != 0) tfIdfTemp = tfIdfTemp + _relevance[i];
                            tfidfDocs.TryAdd(_directories[k], tfIdfTemp);

                        }
                    }
                }
                tfidfDocs.TryAdd(_directories[k], 0); //retorno 0 si no es valido
            }
            queryTitleTfidfReturn.TryAdd(querySplit[i], tfidfDocs);
        }
        return queryTitleTfidfReturn;
    }
    private static float Idf(float numberFiles, float cantRepsDocs)

    {   //formula matematica para calcular IDF

        double f = (numberFiles / 1 + cantRepsDocs);
        float idf = ((float)Math.Log10(f));
        return idf;
    }

    private static float CalculateIDF_extra(string query)
    { //cantidad de repeticiones totales en archivos
        int totalReps = 0;
        for (int k = 0; k < _directories.Length; k++)
        {
            for (int z = 0; z < _titleDocs[_directories[k]].Count(); z++)
            {
                if (query == _titleDocs[_directories[k]][z])
                {
                    totalReps++;
                    z = _titleDocs[_directories[k]].Count();
                }
            }
        }
        return totalReps;
    }

    private static float Tf(float cantReps, float cantTotal)
    {
        float tf = cantReps / cantTotal;
        return tf;
    }

    private static Dictionary<string, float> Final_tfidf(Dictionary<string, Dictionary<string, float>> diccionario)
    {
        //se suman los valores de tfidf por palabra

        Dictionary<string, float> finalReturn = new Dictionary<string, float>();
        for (int k = 0; k < _directories.Length; k++)
        {
            float temp = 0;
            for (int i = 0; i < _querySplit.Count(); i++)
            {
                temp = temp + diccionario[_querySplit[i]][_directories[k]];
            }
            finalReturn.TryAdd(_directories[k], temp);
        }

        return finalReturn;
    }

    private static Dictionary<string, bool> dictionarysBool = DoDiccsBool();
    private static List<string> Organizar_Values(Dictionary<string, float> final)
    {
        //se organizan los valores de manera descendiente y se corrige los cambios en el directorio para poder acceder a el
        List<string> orderReturn = new List<string>();
        var sortedDict = from entry in final orderby entry.Value descending select entry;
        foreach (var value in sortedDict)
        {
            string titleTemp = value.ToString().Remove(0, 1);
            titleTemp = titleTemp.Remove(titleTemp.LastIndexOf(","));
            orderReturn.Add(titleTemp);
        }
        return orderReturn;
    }

    #endregion

    #region Query

    //se inicializan los valores para trabajar con el query y la busqueda

    private static List<string> _queryAnd = new List<string>();
    private static List<string> _queryNearby = new List<string>();
    private static List<string> _queryRelevance = new List<string>();
    private static bool _notBool = false;
    private static bool _andBool = false;
    private static bool _nearbyBool = false;
    private static bool _relevanceBool = false;

    private static bool CheckExistance(string a, string b)
    {
        //se comprueba si una palabra existe en un archivo

        for (int i = 0; i < _titleDocs[a].Count; i++)
        {
            string s = _titleDocs[a][i];
            if ((b == s)) return true;
        }
        return false;
    }

    private static Dictionary<string, bool> DoDiccsBool()
    {
        //se crea el array de booleanos en dependencia de la utilizacion de operadores de negacion y existencia

        Dictionary<string, bool> dicBoolReturn = new Dictionary<string, bool>();
        if (_notBool)
        {
            for (int k = 0; k < _queryNot.Count; k++)
            {
                for (int i = 0; i < _directories.Length; i++)
                {
                    if (k == 0)
                    {
                        if (CheckExistance(_directories[i], _queryNot[k]))
                            dicBoolReturn.TryAdd(_directories[i], true);
                        else
                        {
                            dicBoolReturn.TryAdd(_directories[i], false);
                        }
                    }
                    else
                    {
                        if (CheckExistance(_directories[i], _queryNot[k]) && (!dicBoolReturn[_directories[i]]))
                        {
                            dicBoolReturn.Remove(_directories[i]);
                            dicBoolReturn.Add(_directories[i], true);
                        }
                    }
                }
            }
        }

        if (_notBool && _andBool)
        {
            for (int k = 0; k < _queryAnd.Count; k++)
            {
                for (int i = 0; i < _directories.Length; i++)
                {
                    if (CheckExistance(_directories[i], _queryAnd[k]) && (!dicBoolReturn[_directories[i]]))
                    {
                        dicBoolReturn.Remove(_directories[i]);
                        dicBoolReturn.Add(_directories[i], false);
                    }
                    else
                    {
                        dicBoolReturn.Remove(_directories[i]);
                        dicBoolReturn.Add(_directories[i], true);
                    }
                }
            }
        }

        if (_andBool && !_notBool)
        {
            for (int k = 0; k < _queryAnd.Count; k++)
            {
                for (int i = 0; i < _directories.Length; i++)
                {
                    if (k == 0)
                    {
                        if (CheckExistance(_directories[i], _queryAnd[k]))
                            dicBoolReturn.Add(_directories[i], false);
                        else
                        {
                            dicBoolReturn.Add(_directories[i], true);
                        }
                    }
                    else
                    {
                        if (CheckExistance(_directories[i], _queryAnd[k]) && (!dicBoolReturn[_directories[i]]))
                        {
                            dicBoolReturn.Remove(_directories[i]);
                            dicBoolReturn.Add(_directories[i], false);
                        }
                        else
                        {
                            dicBoolReturn.Remove(_directories[i]);
                            dicBoolReturn.Add(_directories[i], true);
                        }
                    }
                }
            }
        }

        if (!_notBool && !_andBool)
        {
            for (int i = 0; i < _directories.Length; i++)
            {
                dicBoolReturn.Add(_directories[i], false);
            }
        }
        return dicBoolReturn;
    }
    private static float[] _nearbyArray = new float[_directories.Length];
    private static float[] DoNearbyFinal(List<string> query)
    {

        float[] tempCercanos = new float[_directories.Length];
        float[] nearbyFinal = new float[_directories.Length];
        for (int i = 0; i < query.Count; i += 2)
        {
            try
            {
                tempCercanos = DoNearby(query[i], query[i + 1]);
                for (int k = 0; k < nearbyFinal.Length; k++)
                {
                    nearbyFinal[k] = nearbyFinal[k] + tempCercanos[k];
                }
            }
            catch (Exception e) {Cleanup();}

        }
        return nearbyFinal;
    }
    private static float[] DoNearby(string firstWord, string secondWord)
    {
        //se recorren los archivos y se determina la menor cercania de las dos palabras
        
        float[] min = new float[_directories.Length];
        for (int i = 0; i < _directories.Length; i++)
        {
            if (CheckExistance(_directories[i], firstWord) && CheckExistance(_directories[i], secondWord))
            {
                if(firstWord == secondWord)
                {min[i] = 1;}
                else{
                int temp1 = 0;
                int temp2 = 0;
                List<int> farness = new List<int>();
                bool match1 = false;
                bool match2 = false;
                for (int k = 0; k < _titleDocs[_directories[i]].Count; k++)
                {
                    if (firstWord == _titleDocs[_directories[i]][k]) { temp1 = k; match1 = true; }
                    if (secondWord == _titleDocs[_directories[i]][k]) { temp2 = k; match2 = true; }

                    if (match1 && match2)
                    {
                        if ((temp1 - temp2) > 0) farness.Add(temp1 - temp2);
                        if ((temp2 - temp1) > 0) farness.Add(temp2 - temp1);
                        if ((temp1 - temp2) == 0) farness.Add(1);
                        match1 = false; match2 = false;
                        k--;
                    }
                }
                min[i] = 1 / (farness.Min());
            }
            }
            else { min[i] = 0; }
        }
        return min;
    }
    private static List<string> Operator(List<string> query, string operador)
    {
        // se retorna un operador valido para realizar la busqueda
        List<string> queryReturn = new List<string>();
        for (int i = 0; i < query.Count; i++)
        {
            if (query[i].StartsWith(operador)) queryReturn.Add(query[i].ToString().Replace(operador, ""));
        }
        return queryReturn;
    }
    private static List<string> CleanNearbyOperator(List<string> query, string operador)
    {
        List<string> queryReturn = new List<string>();
        for (int i = 0; i < query.Count(); i++)
        {
            try { if (query[i].StartsWith(operador)) { queryReturn.Add(query[i - 1]); queryReturn.Add(query[i + 1]); } }
            catch (Exception e) { Cleanup(); }
        }
        return queryReturn;
    }

    private static bool CheckOperatorBool(string query, string o)
    {
        for (int i = 0; i < query.Length; i++)
        {
            string c = query[i].ToString();
            if ((c == o)) return true;
        }
        return false;
    }
    private static bool CheckOperators(string query)
    {
        for (int i = 0; i < query.Length; i++)
        {
            string c = query[i].ToString();
            if ((c == "!") || (c == "~") || (c == "*") || (c == "^")) return true;
        }
        return false;
    }

    private static List<string> CleanOperatorsFull(List<string> query, string o)
    {
        //se determina con "o" que operador se busca y se elimina del query
        List<string> queryReturn = new List<string>();

        if (_notBool || _andBool)
        {
            for (int i = 0; i < query.Count; i++)
            {
                if (query[i].StartsWith(o)) queryReturn.Add(query[i].ToString()[1..]);
                else { queryReturn.Add(query[i]); }
            }
        }

        return queryReturn;
    }
    private static List<string> CleanOperatorX(List<string> query, string x)
    {
        //se determina con "X" que operador se busca y se elimina del query
        List<string> queryReturn = new List<string>();

        for (int i = 0; i < query.Count; i++)
        {
            if (!query[i].StartsWith(x)) queryReturn.Add(query[i]);
        }
        return queryReturn;
    }
    private static List<string> CleanOperatorRelevance(List<string> query, string o)
    {
        List<string> queryReturn = new List<string>();
        for (int i = 0; i < query.Count; i++)
        {
            int count = query[i].Count(f => f == '*');
            if (count == 1) { _relevance[i] = 2; }
            else { _relevance[i] = count; }

            queryReturn.Add(query[i].Replace("*", ""));
        }
        return queryReturn;
    }

    private static List<string> CleanOperatorRelevanceExtra(List<string> query, string operador)
    {
        List<string> queryReturn = new List<string>();
        for (int i = 0; i < query.Count; i++)
        {
            queryReturn.Add(query[i].Replace(operador, ""));
        }
        return queryReturn;
    }
    #endregion

    #region Snippet
    private static string Snippet(string order, List<string> query)
    {
        //se busca la primera aparicion de la primera palabra y se retorna las palabra a su alrededor (distancia 2)
        for (int i = 0; i < query.Count; i++)
        {
            for (int z = 0; z < _titleDocs[order].Count(); z++)
            {
                if ((query[i] == _titleDocs[order][z]))
                {
                    List<string> temp = new List<string>();
                    if (z == 0 && (z + 2) < _titleDocs[order].Count)
                    {
                        for (int k = z; k < z + 2; k++)
                        {
                            temp.Add(_titleDocs[order][k]);
                        }
                        return String.Join(" ", temp.ToArray());
                    }
                    if (z == _titleDocs[order].Count && (z - 2) >= 0)
                    {
                        for (int k = z - 2; k < z; k++)
                        {
                            temp.Add(_titleDocs[order][k]);
                        }
                        return String.Join(" ", temp.ToArray());
                    }
                    if ((z + 2) <= _titleDocs[order].Count && (z - 2) >= 0)
                    {
                        for (int k = z - 2; k < z + 2; k++)
                        {
                            temp.Add(_titleDocs[order][k]);
                        }
                        return String.Join(" ", temp.ToArray());
                    }
                }
            }
        }

        return query[0]; //de no cumplir las condiciones se retorna el query
    }
    #endregion

    #region Suggestion

    private static string Suggestion()
    {
        //se busca una palabra en los doc con una semejanza suficiente para sugerir
        List<string> toreturn = new List<string>();
        for (int i = 0; i < _querySplit.Count; i++)
        {
            for (int k = 0; k < _directories.Length; k++)
            {
                if(i>=_querySplit.Count) break;
                if (!(dictionarysBool[_directories[k]]))
                {
                    for (int z = 0; z < _titleDocs[_directories[k]].Count; z++)
                    {
                        if (Levinshtain(_querySplit[i], _titleDocs[_directories[k]][z]) < 2 && _querySplit[i] != _titleDocs[_directories[k]][z])
                        {
                            toreturn.Add(_titleDocs[_directories[k]][z]);
                            i++;
                            k = 0;
                            break;
                        }
                    }
                }
            }
        }
        if(toreturn.Count!=0)
        {
            string suggestedExtra = toreturn[0];
            if(toreturn.Count>=2)
            {
                for (int i = 1; i < toreturn.Count; i++)
                {
                    suggestedExtra = suggestedExtra + " " + toreturn[i];
                }
            }
            return suggestedExtra;
        }
        string s = "ni idea"; //no hay coincidencias
        return s;
    }
    private static int Levinshtain(string s, string t)
    {
        //la distancia de Levimcshtain es util para saber que tan parecidas son dos palabras en base a sus caracteres
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1]; //se crea la diagonal

        //se inicializa el array
        for (int i = 0; i <= n; d[i, 0] = i++)
        {
        }

        for (int j = 0; j <= m; d[0, j] = j++)
        {
        }

        // se comienza el bucle para determinar los valores
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                // se computan los valores de la diagonal
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                d[i - 1, j - 1] + cost);
            }
        }
        // y se retornan
        return d[n, m];
    }

    private static void Cleanup()
    {
        _queryNot.Clear();
        _queryRelevance.Clear();
        _querySplit.Clear();
        _queryNearby.Clear();
        _queryAnd.Clear();
        dictionarysBool.Clear();
        _andBool = false;
        _relevanceBool = false;
        _nearbyBool = false;
        _notBool = false;
        for (int i = 0; i < _nearbyArray.Length; i++)
        {
            _nearbyArray[i] = 0;
        }
    }

    #endregion
    private static int New_ItemL(Dictionary<string, float> final, List<string> orden)
    //determina la cantidad de docs que son validos para mostrar
    {
        int temp = 0;
        for (int i = 0; i < _directories.Length; i++)
        {
            if (dictionarysBool[orden[i]] == false && final[orden[i]] != 0) temp++;
        }
        return temp;
    }
}