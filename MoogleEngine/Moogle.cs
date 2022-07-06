using System.Text.RegularExpressions;

namespace MoogleEngine;
public static class Moogle
{
    public static List<string> querySplit = new List<string>();
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
            querySplit = Limpiar(query); //busqueda normal
            dictionarysBool = DoDiccsBool();
        }
        else
        {
            querySplit = Limpiar(query);

            if (CheckOperatorBool(query, "~"))
            {
                nearbyBool = true;
                queryNearby = CleanNearbyOperator(querySplit, "~");
                queryNearby = CleanOperatorRelevanceExtra(queryNearby, "!");
                queryNearby = CleanOperatorRelevanceExtra(queryNearby, "*");
                queryNearby = CleanOperatorRelevanceExtra(queryNearby, "^");
                querySplit = CleanOperatorX(querySplit, "~");
                nearbyArray = DoNearbyFinal(queryNearby);
            }

            if (CheckOperatorBool(query, "*"))
            {
                relevanceBool = true;
                queryRelevance = Operator(querySplit, "*");
                queryRelevance = CleanOperatorRelevanceExtra(queryNearby, "!");
                queryRelevance = CleanOperatorRelevanceExtra(queryNearby, "^");
                querySplit = CleanOperatorRelevance(querySplit, "*");
            }

            if (CheckOperatorBool(query, "!"))
            {
                notBool = true;
                query_not = Operator(querySplit, "!");
                querySplit = CleanOperatorsFull(querySplit, "!");

            }
            if (CheckOperatorBool(query, "^"))
            {
                andBool = true;
                queryAnd = Operator(querySplit, "^");
                querySplit = CleanOperatorsFull(querySplit, "^");
            }

            dictionarysBool = DoDiccsBool();
        }

        //se calcula el tfidf de cada documento por query y se organiza los valores de mayor a menor

        Dictionary<string, Dictionary<string, float>> queryTitleTfidf = doTFIDF(querySplit);
        Dictionary<string, float> final = Final_tfidf(queryTitleTfidf);
        List<string> order = Organizar_Values(final);

        SearchItem[] items = new SearchItem[New_ItemL(final, order)];
        for (int i = 0; i < New_ItemL(final, order); i++)
        {

            if (!dictionarysBool[order[i]] && final[order[i]] != 0)
            {
                string snip = Snippet(order[i], querySplit);
                items[i] = new SearchItem(order[i], snip, final[order[i]]);
            }
        }

        string suggestion = Suggestion();
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
    public static string contentFolder = Path.Join(Environment.CurrentDirectory, "..", "Content/"); //direccion donde estaran los archivos
    public static string[] directories = ObtenerDirectorios(contentFolder);
    public static List<string> query_not = new List<string>();
    public static int numberFiles = directories.Length;
    public static int[] relevance = new int[20];
    public static string[] ñame = new string[20];
    public static Dictionary<string, List<string>> titleDocs = Load();
    public static string[] ObtenerDirectorios(string carga)
    {   //se buscan todos los txt
        string[] directorios = Directory.GetFiles(carga, "*.txt", SearchOption.AllDirectories);
        numberFiles = directorios.Length;
        return directorios;
    }
    public static Dictionary<string, List<string>> Load()
    {   //se cargan las palabras de cada documento sin caracteres especiales para optimizar la busqueda
        Dictionary<string, List<string>> titleDocsReturn = new Dictionary<string, List<string>>();
        List<string> docs = new List<string>();
        for (int i = 0; i < directories.Length; i++)
        {
            StreamReader reader = new StreamReader(directories[i]);
            string temp = reader.ReadToEnd();
            docs = Limpiar_Docs(temp);
            titleDocsReturn.Add(directories[i], docs);
        }
        return titleDocsReturn;
    }
    public static List<string> Limpiar(string words)
    {   //limpieza del query
        return words.ToLower().Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
    }
    public static List<string> Limpiar_Docs(string words)
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

    public static Dictionary<string, Dictionary<string, float>> doTFIDF(List<string> querySplit)
    {
        //calculo de tfidf

        Dictionary<string, Dictionary<string, float>> queryTitleTfidfReturn = new Dictionary<string, Dictionary<string, float>> { };
        float tfIdfTemp = 0;
        for (int i = 0; i < querySplit.Count(); i++) //se recorren las palabras del query
        {

            Dictionary<string, float> tfidfDocs = new Dictionary<string, float>();

            for (int k = 0; k < directories.Length; k++) //se recorren los documentos y se inicializa la cuenta de palabras
            {
                float countQuery = 0;
                if (!dictionarysBool[directories[k]]) //se determina si el documento es valido para analizar o no
                {

                    for (int z = 0; z < titleDocs[directories[k]].Count(); z++)
                    {
                        //coincidencia
                        if ((querySplit[i] == titleDocs[directories[k]][z])) countQuery++;
                        if (z == titleDocs[directories[k]].Count() - 1)
                        {
                            //se llevan a cabo las multiplicaciones en correspondencia con la relevancia de cada archivo

                            tfIdfTemp = TF(countQuery, titleDocs[directories[k]].Count) * IDF(numberFiles, CalculateIDF_extra(querySplit[i]));

                            if (nearbyBool) tfIdfTemp = tfIdfTemp + (nearbyArray[k] * 2);
                            if (relevanceBool && relevance[i] != 0 && tfIdfTemp != 0) tfIdfTemp = tfIdfTemp + relevance[i];
                            tfidfDocs.TryAdd(directories[k], tfIdfTemp);

                        }
                    }
                }
                tfidfDocs.TryAdd(directories[k], 0); //retorno 0 si no es valido
            }
            queryTitleTfidfReturn.TryAdd(querySplit[i], tfidfDocs);
        }
        return queryTitleTfidfReturn;
    }
    private static float IDF(float numberFiles, float cantRepsDocs)

    {   //formula matematica para calcular IDF

        double f = (numberFiles / 1 + cantRepsDocs);
        float idf = ((float)Math.Log10(f));
        return idf;
    }

    private static float CalculateIDF_extra(string query)
    { //cantidad de repeticiones totales en archivos
        int totalReps = 0;
        for (int k = 0; k < directories.Length; k++)
        {
            for (int z = 0; z < titleDocs[directories[k]].Count(); z++)
            {
                if (query == titleDocs[directories[k]][z])
                {
                    totalReps++;
                    z = titleDocs[directories[k]].Count();
                }
            }
        }
        return totalReps;
    }

    private static float TF(float cantReps, float cantTotal)
    {
        float tf = cantReps / cantTotal;
        return tf;
    }

    static public Dictionary<string, float> Final_tfidf(Dictionary<string, Dictionary<string, float>> diccionario)
    {
        //se suman los valores de tfidf por palabra

        Dictionary<string, float> finalReturn = new Dictionary<string, float>();
        for (int k = 0; k < directories.Length; k++)
        {
            float temp = 0;
            for (int i = 0; i < querySplit.Count(); i++)
            {
                temp = temp + diccionario[querySplit[i]][directories[k]];
            }
            finalReturn.TryAdd(directories[k], temp);
        }

        return finalReturn;
    }

    public static Dictionary<string, bool> dictionarysBool = DoDiccsBool();
    public static List<string> Organizar_Values(Dictionary<string, float> final)
    {
        //se organizan los valores de manera descendiente y se corrige los cambios en el directorio para poder acceder a el
        List<string> orderReturn = new List<string>();
        var sortedDict = from entry in final orderby entry.Value descending select entry;
        foreach (var value in sortedDict)
        {
            string title_temp = value.ToString().Remove(0, 1);
            title_temp = title_temp.Remove(title_temp.LastIndexOf(","));
            orderReturn.Add(title_temp);
        }
        return orderReturn;
    }

    #endregion

    #region Query

    //se inicializan los valores para trabajar con el query y la busqueda

    public static List<string> queryAnd = new List<string>();
    public static List<string> queryNearby = new List<string>();
    public static List<string> queryRelevance = new List<string>();
    public static bool notBool = false;
    public static bool andBool = false;
    public static bool nearbyBool = false;
    public static bool relevanceBool = false;

    private static bool CheckExistance(string a, string b)
    {
        //se comprueba si una palabra existe en un archivo

        for (int i = 0; i < titleDocs[a].Count; i++)
        {
            string s = titleDocs[a][i];
            if ((b == s)) return true;
        }
        return false;
    }

    public static Dictionary<string, bool> DoDiccsBool()
    {
        //se crea el array de booleanos en dependencia de la utilizacion de operadores de negacion y existencia

        Dictionary<string, bool> dicBoolReturn = new Dictionary<string, bool>();
        if (notBool)
        {
            for (int k = 0; k < query_not.Count; k++)
            {
                for (int i = 0; i < directories.Length; i++)
                {
                    if (k == 0)
                    {
                        if (CheckExistance(directories[i], query_not[k]))
                            dicBoolReturn.TryAdd(directories[i], true);
                        else
                        {
                            dicBoolReturn.TryAdd(directories[i], false);
                        }
                    }
                    else
                    {
                        if (CheckExistance(directories[i], query_not[k]) && (!dicBoolReturn[directories[i]]))
                        {
                            dicBoolReturn.Remove(directories[i]);
                            dicBoolReturn.Add(directories[i], true);
                        }
                    }
                }
            }
        }

        if (notBool && andBool)
        {
            for (int k = 0; k < queryAnd.Count; k++)
            {
                for (int i = 0; i < directories.Length; i++)
                {
                    if (CheckExistance(directories[i], queryAnd[k]) && (!dicBoolReturn[directories[i]]))
                    {
                        dicBoolReturn.Remove(directories[i]);
                        dicBoolReturn.Add(directories[i], false);
                    }
                    else
                    {
                        dicBoolReturn.Remove(directories[i]);
                        dicBoolReturn.Add(directories[i], true);
                    }
                }
            }
        }

        if (andBool && !notBool)
        {
            for (int k = 0; k < queryAnd.Count; k++)
            {
                for (int i = 0; i < directories.Length; i++)
                {
                    if (k == 0)
                    {
                        if (CheckExistance(directories[i], queryAnd[k]))
                            dicBoolReturn.Add(directories[i], false);
                        else
                        {
                            dicBoolReturn.Add(directories[i], true);
                        }
                    }
                    else
                    {
                        if (CheckExistance(directories[i], queryAnd[k]) && (!dicBoolReturn[directories[i]]))
                        {
                            dicBoolReturn.Remove(directories[i]);
                            dicBoolReturn.Add(directories[i], false);
                        }
                        else
                        {
                            dicBoolReturn.Remove(directories[i]);
                            dicBoolReturn.Add(directories[i], true);
                        }
                    }
                }
            }
        }

        if (!notBool && !andBool)
        {
            for (int i = 0; i < directories.Length; i++)
            {
                dicBoolReturn.Add(directories[i], false);
            }
        }
        return dicBoolReturn;
    }
    public static float[] nearbyArray = new float[directories.Length];
    public static float[] DoNearbyFinal(List<string> query)
    {

        float[] tempCercanos = new float[directories.Length];
        float[] nearbyFinal = new float[directories.Length];
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
    public static float[] DoNearby(string firstWord, string secondWord)
    {
        //se recorren los archivos y se determina la menor cercania de las dos palabras
        
        float[] min = new float[directories.Length];
        for (int i = 0; i < directories.Length; i++)
        {
            if (CheckExistance(directories[i], firstWord) && CheckExistance(directories[i], secondWord))
            {
                if(firstWord == secondWord)
                {min[i] = 1;}
                else{
                int temp1 = 0;
                int temp2 = 0;
                List<int> farness = new List<int>();
                bool match1 = false;
                bool match2 = false;
                for (int k = 0; k < titleDocs[directories[i]].Count; k++)
                {
                    if (firstWord == titleDocs[directories[i]][k]) { temp1 = k; match1 = true; }
                    if (secondWord == titleDocs[directories[i]][k]) { temp2 = k; match2 = true; }

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
    public static List<string> Operator(List<string> query, string o)
    {
        // se retorna un operador valido para realizar la busqueda
        List<string> queryReturn = new List<string>();
        for (int i = 0; i < query.Count; i++)
        {
            if (query[i].StartsWith(o)) queryReturn.Add(query[i].ToString().Replace(o, ""));
        }
        return queryReturn;
    }
    public static List<string> CleanNearbyOperator(List<string> query, string o)
    {
        List<string> query_return = new List<string>();
        for (int i = 0; i < query.Count(); i++)
        {
            try { if (query[i].StartsWith(o)) { query_return.Add(query[i - 1]); query_return.Add(query[i + 1]); } }
            catch (Exception e) { Cleanup(); }
        }
        return query_return;
    }

    public static bool CheckOperatorBool(string query, string o)
    {
        for (int i = 0; i < query.Length; i++)
        {
            string c = query[i].ToString();
            if ((c == o)) return true;
        }
        return false;
    }
    public static bool CheckOperators(string query)
    {
        for (int i = 0; i < query.Length; i++)
        {
            string c = query[i].ToString();
            if ((c == "!") || (c == "~") || (c == "*") || (c == "^")) return true;
        }
        return false;
    }

    public static List<string> CleanOperatorsFull(List<string> query, string o)
    {
        //se determina con "o" que operador se busca y se elimina del query
        List<string> query_return = new List<string>();

        if (notBool || andBool)
        {
            for (int i = 0; i < query.Count; i++)
            {
                if (query[i].StartsWith(o)) query_return.Add(query[i].ToString()[1..]);
                else { query_return.Add(query[i]); }
            }
        }

        return query_return;
    }
    public static List<string> CleanOperatorX(List<string> query, string X)
    {
        //se determina con "X" que operador se busca y se elimina del query
        List<string> queryReturn = new List<string>();

        for (int i = 0; i < query.Count; i++)
        {
            if (!query[i].StartsWith(X)) queryReturn.Add(query[i]);
        }
        return queryReturn;
    }
    public static List<string> CleanOperatorRelevance(List<string> query, string o)
    {
        List<string> queryReturn = new List<string>();
        for (int i = 0; i < query.Count; i++)
        {
            int count = query[i].Count(f => f == '*');
            if (count == 1) { relevance[i] = 2; }
            else { relevance[i] = count; }

            queryReturn.Add(query[i].Replace("*", ""));
        }
        return queryReturn;
    }

    public static List<string> CleanOperatorRelevanceExtra(List<string> query, string o)
    {
        List<string> query_return = new List<string>();
        for (int i = 0; i < query.Count; i++)
        {
            query_return.Add(query[i].Replace(o, ""));
        }
        return query_return;
    }
    #endregion

    #region Snippet
    public static string Snippet(string order, List<string> query)
    {
        //se busca la primera aparicion de la primera palabra y se retorna las palabra a su alrededor (distancia 2)
        for (int i = 0; i < query.Count; i++)
        {
            for (int z = 0; z < titleDocs[order].Count(); z++)
            {
                if ((query[i] == titleDocs[order][z]))
                {
                    List<string> temp = new List<string>();
                    if (z == 0 && (z + 2) < titleDocs[order].Count)
                    {
                        for (int k = z; k < z + 2; k++)
                        {
                            temp.Add(titleDocs[order][k]);
                        }
                        return String.Join(" ", temp.ToArray());
                    }
                    if (z == titleDocs[order].Count && (z - 2) >= 0)
                    {
                        for (int k = z - 2; k < z; k++)
                        {
                            temp.Add(titleDocs[order][k]);
                        }
                        return String.Join(" ", temp.ToArray());
                    }
                    if ((z + 2) <= titleDocs[order].Count && (z - 2) >= 0)
                    {
                        for (int k = z - 2; k < z + 2; k++)
                        {
                            temp.Add(titleDocs[order][k]);
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

    public static string Suggestion()
    {
        //se busca una palabra en los doc con una semejanza suficiente para sugerir
        List<string> toreturn = new List<string>();
        for (int i = 0; i < querySplit.Count; i++)
        {
            for (int k = 0; k < directories.Length; k++)
            {
                if(i>=querySplit.Count) break;
                if (!(dictionarysBool[directories[k]]))
                {
                    for (int z = 0; z < titleDocs[directories[k]].Count; z++)
                    {
                        if (Levinshtain(querySplit[i], titleDocs[directories[k]][z]) < 2 && querySplit[i] != titleDocs[directories[k]][z])
                        {
                            toreturn.Add(titleDocs[directories[k]][z]);
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
    public static int Levinshtain(string s, string t)
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

    public static void Cleanup()
    {
        query_not.Clear();
        queryRelevance.Clear();
        querySplit.Clear();
        queryNearby.Clear();
        queryAnd.Clear();
        dictionarysBool.Clear();
        andBool = false;
        relevanceBool = false;
        nearbyBool = false;
        notBool = false;
        for (int i = 0; i < nearbyArray.Length; i++)
        {
            nearbyArray[i] = 0;
        }
    }

    #endregion
    public static int New_ItemL(Dictionary<string, float> final, List<string> orden)
    //determina la cantidad de docs que son validos para mostrar
    {
        int temp = 0;
        for (int i = 0; i < directories.Length; i++)
        {
            if (dictionarysBool[orden[i]] == false && final[orden[i]] != 0) temp++;
        }
        return temp;
    }
}