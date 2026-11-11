namespace KrizevciBus.Api.Services;

public static class Geo
{
    // Haversine udaljenost u metrima, točke su [lat, lng]
    public static double Distance(double[] a, double[] b)
    {
        const double R = 6371000;
        var dLat = ToRad(b[0] - a[0]);
        var dLng = ToRad(b[1] - a[1]);
        var h = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(ToRad(a[0])) * Math.Cos(ToRad(b[0])) * Math.Pow(Math.Sin(dLng / 2), 2);
        return 2 * R * Math.Asin(Math.Sqrt(h));
    }

    public static int NearestIndex(List<double[]> points, double lat, double lng, int from = 0)
    {
        int best = from;
        double bestDistance = double.MaxValue;
        double[] target = [lat, lng];
        for (int i = from; i < points.Count; i++)
        {
            var d = Distance(points[i], target);
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        return best;
    }

    static double ToRad(double deg) => deg * Math.PI / 180;
}
